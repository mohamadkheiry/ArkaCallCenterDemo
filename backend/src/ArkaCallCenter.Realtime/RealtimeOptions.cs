namespace ArkaCallCenter.Realtime;

/// <summary>تنظیمات worker پل صوتی.</summary>
public class RealtimeOptions
{
    /// <summary>پورت TCP که سرور AudioSocket روی آن گوش می‌دهد (Asterisk به آن وصل می‌شود).</summary>
    public int AudioSocketPort { get; set; } = 9092;

    /// <summary>آدرس bind سرور AudioSocket.</summary>
    public string AudioSocketHost { get; set; } = "0.0.0.0";
}
