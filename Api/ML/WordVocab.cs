namespace PokeChat.ML;

public class WordVocab
{
    public const int DefaultMaxSize = 2000;
    public const string UnkToken = "<unk>";
    public const string EosToken = "<eos>";
    public const string BosToken = "<bos>";

    private readonly Dictionary<string, int> _wordToIndex = new();
    private readonly Dictionary<int, string> _indexToWord = new();
    private readonly Dictionary<string, int> _wordCounts = new();
    private int _nextIndex;

    public int Size => _wordToIndex.Count;
    public IReadOnlyDictionary<string, int> WordToIndex => _wordToIndex;
    public IReadOnlyDictionary<int, string> IndexToWord => _indexToWord;

    public WordVocab()
    {
        AddToken(BosToken);
        AddToken(EosToken);
        AddToken(UnkToken);
    }

    public int AddToken(string word)
    {
        var lower = word.ToLowerInvariant();
        if (_wordToIndex.TryGetValue(lower, out var existing))
            return existing;
        var idx = _nextIndex++;
        _wordToIndex[lower] = idx;
        _indexToWord[idx] = lower;
        return idx;
    }

    public int GetIndex(string word)
    {
        var lower = word.ToLowerInvariant();
        return _wordToIndex.TryGetValue(lower, out var idx) ? idx : _wordToIndex[UnkToken];
    }

    public string GetWord(int index) =>
        _indexToWord.TryGetValue(index, out var word) ? word : UnkToken;

    public void CountWord(string word)
    {
        var lower = word.ToLowerInvariant();
        _wordCounts.TryGetValue(lower, out var count);
        _wordCounts[lower] = count + 1;
    }

    public void BuildFromCounts(int maxSize = DefaultMaxSize)
    {
        var sorted = _wordCounts.OrderByDescending(kv => kv.Value).Take(maxSize - 3);
        foreach (var kv in sorted)
            AddToken(kv.Key);
    }

    public int[] Tokenise(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var indices = new int[words.Length + 1];
        for (int i = 0; i < words.Length; i++)
            indices[i] = GetIndex(words[i]);
        indices[^1] = GetIndex(EosToken);
        return indices;
    }

    public string Detokenise(int[] indices)
    {
        var words = new List<string>();
        foreach (var idx in indices)
        {
            var word = GetWord(idx);
            if (word == EosToken || word == BosToken) break;
            if (word == UnkToken) continue;
            words.Add(word);
        }
        return string.Join(" ", words);
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var writer = new BinaryWriter(File.OpenWrite(path));
        writer.Write(_wordToIndex.Count);
        foreach (var kv in _wordToIndex)
        {
            writer.Write(kv.Key);
            writer.Write(kv.Value);
        }
    }

    public static WordVocab? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            var count = reader.ReadInt32();
            var vocab = new WordVocab();
            vocab._wordToIndex.Clear();
            vocab._indexToWord.Clear();
            vocab._nextIndex = 0;
            for (int i = 0; i < count; i++)
            {
                var word = reader.ReadString();
                var idx = reader.ReadInt32();
                vocab._wordToIndex[word] = idx;
                vocab._indexToWord[idx] = word;
                if (idx >= vocab._nextIndex) vocab._nextIndex = idx + 1;
            }
            return vocab;
        }
        catch
        {
            return null;
        }
    }
}
