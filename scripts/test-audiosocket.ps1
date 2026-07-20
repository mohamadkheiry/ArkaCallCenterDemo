param(
    [ValidateRange(1, 999999)]
    [int]$Extension = 2,
    [string]$HostName = "127.0.0.1",
    [ValidateRange(1, 30)]
    [int]$DurationSeconds = 8,
    [string]$InputSlinPath
)

$client = [System.Net.Sockets.TcpClient]::new()
try {
    $client.Connect($HostName, 9092)
    $stream = $client.GetStream()
    $stream.ReadTimeout = 1000

    # AudioSocket IDs encode the decimal extension in the final 12 hex digits.
    $extensionDigits = $Extension.ToString("000000000000")
    $uuid = [byte[]]::new(16)
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

    if (-not [string]::IsNullOrWhiteSpace($InputSlinPath)) {
        $resolvedInput = (Resolve-Path -LiteralPath $InputSlinPath).Path
        $inputAudio = [System.IO.File]::ReadAllBytes($resolvedInput)
        Start-Sleep -Milliseconds 2500
        for ($offset = 0; $offset -lt $inputAudio.Length; $offset += 320) {
            $size = [Math]::Min(320, $inputAudio.Length - $offset)
            $audioFrame = [byte[]]::new(3 + $size)
            $audioFrame[0] = 0x10
            $audioFrame[1] = [byte](($size -shr 8) -band 0xff)
            $audioFrame[2] = [byte]($size -band 0xff)
            [Array]::Copy($inputAudio, $offset, $audioFrame, 3, $size)
            $stream.Write($audioFrame, 0, $audioFrame.Length)
            Start-Sleep -Milliseconds 20
        }
        # Asterisk keeps sending silent audio; preserve that behavior so server VAD
        # can close the caller turn and request a transcript.
        $silenceFrame = [byte[]]::new(323)
        $silenceFrame[0] = 0x10
        $silenceFrame[1] = 0x01
        $silenceFrame[2] = 0x40
        for ($index = 0; $index -lt 100; $index++) {
            $stream.Write($silenceFrame, 0, $silenceFrame.Length)
            Start-Sleep -Milliseconds 20
        }
    }

    while ($watch.Elapsed.TotalSeconds -lt $DurationSeconds) {
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

    [pscustomobject]@{
        extension = $Extension
        firstAudibleMs = $firstAudibleMs
        audioFrames = $audioFrames
        audibleFrames = $audibleFrames
    } | ConvertTo-Json -Compress

    if ($firstAudibleMs -lt 0) { exit 1 }
}
finally {
    if ($null -ne $stream) { $stream.Dispose() }
    $client.Dispose()
}
