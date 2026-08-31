namespace DoAnOlympics.Api.Validators;

public static class Iso6346Validator
{
    private static readonly Dictionary<char, int> LetterValues = BuildLetterValues();

    private static Dictionary<char, int> BuildLetterValues()
    {
        var map = new Dictionary<char, int>();
        int value = 10;
        foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            while (value % 11 == 0) value++;
            map[c] = value;
            value++;
        }
        return map;
    }

    public static bool IsValid(string maContainer, out string loiNeuCo)
    {
        loiNeuCo = string.Empty;

        if (string.IsNullOrWhiteSpace(maContainer))
        {
            loiNeuCo = "Mã container rỗng";
            return false;
        }

        string ma = maContainer.Trim().ToUpperInvariant().Replace(" ", "");

        if (ma.Length != 11)
        {
            loiNeuCo = $"Độ dài phải là 11 ký tự, hiện tại là {ma.Length} ('{ma}')";
            return false;
        }

        if (!ma[..4].All(char.IsLetter))
        {
            loiNeuCo = "4 ký tự đầu phải là chữ cái";
            return false;
        }

        if (!ma[4..].All(char.IsDigit))
        {
            loiNeuCo = "7 ký tự cuối phải là số";
            return false;
        }

        int tong = 0;
        for (int i = 0; i < 10; i++)
        {
            char c = ma[i];
            int giaTri = char.IsLetter(c) ? LetterValues[c] : c - '0';
            tong += giaTri * (int)Math.Pow(2, i);
        }

        int checkDigitTinhDuoc = tong % 11;
        if (checkDigitTinhDuoc == 10) checkDigitTinhDuoc = 0;

        int checkDigitThucTe = ma[10] - '0';

        if (checkDigitTinhDuoc != checkDigitThucTe)
        {
            loiNeuCo = $"Checksum sai: tính được {checkDigitTinhDuoc}, mã ghi {checkDigitThucTe}";
            return false;
        }

        return true;
    }
}
