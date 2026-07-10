namespace PokeChat.Stories;

public static class SyllableCounter
{
    private static readonly HashSet<string> Exceptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "are", "were", "there", "here", "fire", "hire", "wire", "tire",
        "gone", "done", "one", "once", "bye", "rye", "lye", "dye", "eye",
        "flower", "power", "tower", "shower", "lower", "our", "hour", "every",
        "area", "idea", "radio", "video", "studio", "curious", "serious", "obvious",
        "previous", "various", "period", "royal", "loyal", "trial", "dial", "fuel",
        "cruel", "poem", "poet", "being", "seeing", "going", "doing",
        "higher", "lower", "fire", "hire", "wire", "tire", "liar",
        "hundred",
    };

    public static int Count(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return 0;

        var lower = word.Trim().ToLowerInvariant();

        if (Exceptions.Contains(lower))
            return ExceptionsSyllables(lower);

        var vowelCount = 0;
        var prevIsVowel = false;
        var len = lower.Length;

        for (var i = 0; i < len; i++)
        {
            var ch = lower[i];
            var isVowel = IsVowel(ch, i, len, lower);

            if (isVowel && !prevIsVowel)
                vowelCount++;
            prevIsVowel = isVowel;
        }

        vowelCount = AdjustForSilentE(lower, vowelCount);
        vowelCount = AdjustForEdEnding(lower, vowelCount);
        vowelCount = AdjustForLeEnding(lower, vowelCount);
        vowelCount = AdjustForSmEnding(lower, vowelCount);
        vowelCount = AdjustForDiphthongs(lower, vowelCount);

        return System.Math.Max(1, vowelCount);
    }

    private static int ExceptionsSyllables(string word)
    {
        return word switch
        {
            "the" or "are" or "were" or "there" or "here" or "fire" or "hire" or
            "wire" or "tire" or "gone" or "done" or "one" or "once" or "bye" or
            "rye" or "lye" or "dye" or "eye" or "liar" => 1,
            "flower" or "power" or "tower" or "shower" or "lower" or "our" or
            "hour" or "every" or "poem" or "poet" or "being" or "seeing" or
            "going" or "doing" or "higher" or "lower" or "royal" or "loyal" or
            "trial" or "dial" or "fuel" or "cruel" => 2,
            "area" or "idea" or "radio" or "video" or "studio" or "curious" or
            "serious" or "obvious" or "previous" or "various" or "period" => 3,
            _ => 2
        };
    }

    private static bool IsVowel(char ch, int index, int len, string word)
    {
        if ("aeiou".Contains(ch))
            return true;
        if (ch == 'y' && index > 0 && index < len - 1)
            return true;
        if (ch == 'y' && index == len - 1 && len > 2)
            return true;
        return false;
    }

    private static int AdjustForSilentE(string word, int count)
    {
        if (word.Length < 3) return count;
        if (!word.EndsWith('e')) return count;
        if (word.EndsWith("le")) return count;

        var stem = word[..^1];
        var stemVowels = CountSimpleVowels(stem);
        if (stemVowels >= 1 && CountSimpleVowels(word) > stemVowels)
            count--;

        return count;
    }

    private static int AdjustForEdEnding(string word, int count)
    {
        if (word.Length < 4) return count;
        if (!word.EndsWith("ed")) return count;
        if (word.EndsWith("eed")) return count;

        var stem = word[..^2];
        var stemEnd = stem.Length > 0 ? stem[^1] : ' ';
        if (stemEnd == 'd' || stemEnd == 't')
            return count;
        return count - 1;
    }

    private static int AdjustForLeEnding(string word, int count)
    {
        if (word.Length < 4) return count;
        if (!word.EndsWith("le")) return count;

        var beforeLe = word[^3];
        if ("aeiou".Contains(beforeLe)) return count;

        if (count == 0) return 1;
        return count;
    }

    private static int AdjustForSmEnding(string word, int count)
    {
        if (word.EndsWith("ism") && word.Length > 5)
            return count + 1;
        if (word.EndsWith("sm") && word.Length > 4)
            return count + 1;
        return count;
    }

    private static int AdjustForDiphthongs(string word, int count)
    {
        if (word.Contains("ae") || word.Contains("oe")) count--;
        return count;
    }

    private static int CountSimpleVowels(string s)
    {
        var count = 0;
        foreach (var ch in s)
        {
            if ("aeiouy".Contains(ch))
                count++;
        }
        return count;
    }
}
