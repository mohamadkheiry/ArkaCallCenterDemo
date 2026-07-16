using System.Text;
using System.Text.RegularExpressions;

namespace ArkaCallCenter.Infrastructure.Text;

/// <summary>
/// اعداد داخل متن را به حروفِ فارسیِ اعراب‌دار تبدیل می‌کند تا هوش مصنوعی/TTS آن‌ها را
/// درست تلفظ کند. منطقِ «هوشمند»:
/// <list type="bullet">
/// <item>شماره‌ها/شناسه‌ها (صفرِ ابتدایی یا طولِ ≥ ۱۰: موبایل، تلفن، کد ملی، کارت) رقم‌به‌رقم.</item>
/// <item>مقادیر (قیمت، درصد، تعداد) به‌صورتِ عددِ کامل (یک میلیون و پانصد هزار).</item>
/// <item>اعشار با «ممیز» و بخشِ اعشاری رقم‌به‌رقم.</item>
/// </list>
/// </summary>
public static class PersianNumberWords
{
    // ارقامِ تک با اعراب (مطابقِ تلفظِ موردِ تأیید).
    private static readonly string[] Ones =
        { "صِفر", "یِک", "دو", "سه", "چَهار", "پَنج", "شِش", "هَفت", "هَشت", "نُه" };
    private static readonly string[] Teens =
        { "دَه", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده" };
    // اندیس ۲..۹ استفاده می‌شود.
    private static readonly string[] Tens =
        { "", "", "بیست", "سی", "چِهِل", "پَنجاه", "شَصت", "هَفتاد", "هَشتاد", "نَوَد" };
    // اندیس ۱..۹ استفاده می‌شود.
    private static readonly string[] Hundreds =
        { "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "شِشصد", "هفتصد", "هشتصد", "نهصد" };
    private static readonly string[] Scales = { "", "هزار", "میلیون", "میلیارد", "بیلیون" };

    private static readonly Regex NumberRx = new(@"\d+(?:[.٫]\d+)?", RegexOptions.Compiled);
    private static readonly Regex ThousandSepRx = new(@"(?<=\d)[,٬،](?=\d)", RegexOptions.Compiled);
    private static readonly Regex HSpaceRx = new(@"[ \t]{2,}", RegexOptions.Compiled);
    // فاصله‌ی افتاده قبل از نشانه‌های نگارشی (پس از تبدیل عدد به حروف) حذف شود.
    private static readonly Regex SpaceBeforePunctRx = new(@"[ \t]+(?=[،؛.,!؟:)\]])", RegexOptions.Compiled);

    /// <summary>همه‌ی اعداد را در متن به حروف تبدیل می‌کند (ارقامِ فارسی/عربی هم پشتیبانی می‌شوند).</summary>
    public static string Convert(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";

        // ۱) ارقامِ فارسی/عربی → اسکی
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));      // فارسی ۰-۹
            else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠'))); // عربی ٠-٩
            else sb.Append(ch);
        }
        var t = sb.ToString();

        // ۲) حذفِ جداکننده‌ی هزارگان بینِ ارقام (,) (٬) (،)
        t = ThousandSepRx.Replace(t, "");

        // ۳) تبدیلِ هر عدد به حروف؛ با فاصله‌ی محافظ تا به حروفِ چسبیده گلوله نشود.
        t = NumberRx.Replace(t, m => " " + TokenToWords(m.Value) + " ");

        // ۴) جمع‌کردنِ فاصله‌های افقیِ اضافه (بدون دست‌زدن به خطوطِ جدید) و حذفِ فاصله‌ی قبل از نگارش
        t = HSpaceRx.Replace(t, " ");
        t = SpaceBeforePunctRx.Replace(t, "");
        return t;
    }

    private static string TokenToWords(string token)
    {
        var dot = token.IndexOfAny(new[] { '.', '٫' });
        if (dot < 0) return IntegerToWords(token);

        var intPart = token[..dot];
        var fracPart = token[(dot + 1)..];
        var head = IntegerToWords(intPart.Length == 0 ? "0" : intPart);
        var frac = SpellDigits(fracPart);
        return $"{head} ممیز {frac}".Trim();
    }

    /// <summary>یک رشته‌ی صحیح را طبقِ منطقِ هوشمند به حروف تبدیل می‌کند.</summary>
    private static string IntegerToWords(string digits)
    {
        if (digits.Length == 0) return "";
        // شماره/شناسه: صفرِ ابتدایی یا طولِ ≥ ۱۰ → رقم‌به‌رقم.
        if (digits[0] == '0' || digits.Length >= 10)
            return SpellDigits(digits);
        return Cardinal(long.Parse(digits));
    }

    /// <summary>خواندنِ رقم‌به‌رقم (برای شماره‌ها).</summary>
    private static string SpellDigits(string digits)
    {
        var parts = new List<string>(digits.Length);
        foreach (var c in digits)
            if (c >= '0' && c <= '9') parts.Add(Ones[c - '0']);
        return string.Join(" ", parts);
    }

    /// <summary>خواندنِ عددِ کامل (برای قیمت/تعداد/درصد).</summary>
    private static string Cardinal(long n)
    {
        if (n == 0) return Ones[0];
        // گروه‌بندیِ سه‌رقمی از راست
        var groups = new List<int>();
        while (n > 0) { groups.Add((int)(n % 1000)); n /= 1000; }

        var segments = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var g = groups[i];
            if (g == 0) continue;
            var w = ThreeToWords(g);
            if (i > 0) w += " " + Scales[i];
            segments.Add(w);
        }
        return string.Join(" و ", segments);
    }

    /// <summary>عددِ ۱..۹۹۹ به حروف.</summary>
    private static string ThreeToWords(int n)
    {
        var parts = new List<string>(3);
        var h = n / 100;
        var r = n % 100;
        if (h > 0) parts.Add(Hundreds[h]);
        if (r > 0)
        {
            if (r < 10) parts.Add(Ones[r]);
            else if (r < 20) parts.Add(Teens[r - 10]);
            else
            {
                parts.Add(Tens[r / 10]);
                if (r % 10 > 0) parts.Add(Ones[r % 10]);
            }
        }
        return string.Join(" و ", parts);
    }
}
