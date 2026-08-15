param(
    [ValidateRange(1, 999999)]
    [int]$Extension = 2,
    [string]$HostName = "127.0.0.1",
    [ValidateRange(1, 60)]
    [int]$DurationSeconds = 8,
    [string]$InputSlinPath,
    [ValidateRange(0, 30000)]
    [int]$InputDelayMilliseconds = 2500,
    [ValidatePattern('^\d{1,19}$')]
    [string]$CallerId,
    [string]$OutputSlinPath
)

$client = [System.Net.Sockets.TcpClient]::new()
$capturedAudio = if ([string]::IsNullOrWhiteSpace($OutputSlinPath)) { $null } else { [System.IO.MemoryStream]::new() }
try {
    $client.Connect($HostName, 9092)
    $stream = $client.GetStream()
    $stream.ReadTimeout = 1000

    # AudioSocket IDs encode the decimal extension in the final 12 hex digits.
    $extensionDigits = $Extension.ToString("000000000000")
    $uuid = [byte[]]::new(16)
    if (-not [string]::IsNullOrWhiteSpace($CallerId)) {
        # The first 20 hex digits are decimal BCD-like digits: zero padding,
        # a sentinel 1, then the caller number. This mirrors the production dialplan.
        $callerDigits = ("1" + $CallerId).PadLeft(20, "0")
        for ($index = 0; $index -lt 10; $index++) {
            $uuid[$index] = [Convert]::ToByte($callerDigits.Substring($index * 2, 2), 16)
        }
    }
    for ($index = 0; $index -lt 6; $index++) {
        $uuid[10 + $index] = [Convert]::ToByte($extensionDigits.Substring($index * 2, 2), 16)
    }

    $idFrame = [byte[]]::new(19)
    $idFrame[0] = 0x01
    $idFrame[1] = 0x00
    $idFrame[2] = 0x10
    [Array]::Copy($uuid, 0, $idFrame, 3, $uuid.Length)
    $stream.Write($idFrame, 0, $idFrame.Length)

    $watch = [Diagnostics.Stopwatch]::StartNew()
    $firstAudibleMs = -1
    $audioFrames = 0
    $audibleFrames = 0
    $inputFramesSent = 0
    $inputAudio = $null
    $inputOffset = 0
    $silenceFramesRemaining = 0
    $nextInputFrameAtMs = [double]$InputDelayMilliseconds

    if (-not [string]::IsNullOrWhiteSpace($InputSlinPath)) {
        $resolvedInput = (Resolve-Path -LiteralPath $InputSlinPath).Path
        $inputAudio = [System.IO.File]::ReadAllBytes($resolvedInput)
        $silenceFramesRemaining = 100
    }

    # Read and write on one short-interval event loop. Reading continuously is
    # important: an 8-second welcome is larger than a typical TCP receive buffer,
    # and waiting to read until after input can stall welcome playback on the server.
    while ($watch.Elapsed.TotalSeconds -lt $DurationSeconds) {
        $didWork = $false

        # Keep the caller stream close to Asterisk's 20 ms clock without blocking
        # receipt of welcome/assistant audio.
        $framesThisIteration = 0
        while ($null -ne $inputAudio -and
               $watch.Elapsed.TotalMilliseconds -ge $nextInputFrameAtMs -and
               $framesThisIteration -lt 4) {
            if ($inputOffset -lt $inputAudio.Length) {
                $size = [Math]::Min(320, $inputAudio.Length - $inputOffset)
                $audioFrame = [byte[]]::new(3 + $size)
                $audioFrame[0] = 0x10
                $audioFrame[1] = [byte](($size -shr 8) -band 0xff)
                $audioFrame[2] = [byte]($size -band 0xff)
                [Array]::Copy($inputAudio, $inputOffset, $audioFrame, 3, $size)
                $inputOffset += $size
            }
            elseif ($silenceFramesRemaining -gt 0) {
                # Asterisk keeps sending silent audio. Preserve two seconds of it
                # so server VAD can close the caller turn and request a transcript.
                $audioFrame = [byte[]]::new(323)
                $audioFrame[0] = 0x10
                $audioFrame[1] = 0x01
                $audioFrame[2] = 0x40
                $silenceFramesRemaining--
            }
            else {
                $inputAudio = $null
                break
            }

            $stream.Write($audioFrame, 0, $audioFrame.Length)
            $inputFramesSent++
            $framesThisIteration++
            $nextInputFrameAtMs += 20
            $didWork = $true
        }

        if (-not $stream.DataAvailable) {
            if (-not $didWork) { Start-Sleep -Milliseconds 2 }
            continue
        }

        try {
            $header = [byte[]]::new(3)
            $headerRead = 0
            while ($headerRead -lt $header.Length) {
                $count = $stream.Read($header, $headerRead, $header.Length - $headerRead)
                if ($count -eq 0) { break }
                $headerRead += $count
            }
            if ($headerRead -lt $header.Length) { break }

            $payloadLength = ($header[1] * 256) + $header[2]
            $payload = [byte[]]::new($payloadLength)
            $payloadRead = 0
            while ($payloadRead -lt $payloadLength) {
                $count = $stream.Read($payload, $payloadRead, $payloadLength - $payloadRead)
                if ($count -eq 0) { break }
                $payloadRead += $count
            }

            if ($header[0] -ne 0x10) { continue }
            if ($null -ne $capturedAudio) {
                $capturedAudio.Write($payload, 0, $payload.Length)
            }
            $audioFrames++
            $audible = $false
            for ($index = 0; $index + 1 -lt $payload.Length; $index += 2) {
                $sample = [BitConverter]::ToInt16($payload, $index)
                if ([Math]::Abs([int]$sample) -gt 150) {
                    $audible = $true
                    break
                }
            }
            if (-not $audible) { continue }
            $audibleFrames++
            if ($firstAudibleMs -lt 0) { $firstAudibleMs = [int]$watch.Elapsed.TotalMilliseconds }
        }
        catch [System.IO.IOException] { continue }
    }

    $hangupFrame = [byte[]](0x00, 0x00, 0x00)
    $stream.Write($hangupFrame, 0, $hangupFrame.Length)

    if ($null -ne $capturedAudio) {
        [System.IO.File]::WriteAllBytes(
            [System.IO.Path]::GetFullPath($OutputSlinPath),
            $capturedAudio.ToArray())
    }

    [pscustomobject]@{
        extension = $Extension
        callerId = $CallerId
        inputFramesSent = $inputFramesSent
        firstAudibleMs = $firstAudibleMs
        audioFrames = $audioFrames
        audibleFrames = $audibleFrames
        capturedAudioBytes = if ($null -eq $capturedAudio) { 0 } else { $capturedAudio.Length }
    } | ConvertTo-Json -Compress

    if ($firstAudibleMs -lt 0) { exit 1 }
}
finally {
    if ($null -ne $capturedAudio) { $capturedAudio.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
    $client.Dispose()
}
