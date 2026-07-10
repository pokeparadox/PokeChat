namespace PokeChat.ML;

public class SimpleNeuralNet
{
    private readonly int _inputSize;
    private readonly int _hiddenSize;
    private readonly int _outputSize;
    private float[,] _w1;
    private float[] _b1;
    private float[,] _w2;
    private float[] _b2;

    public SimpleNeuralNet(int inputSize, int hiddenSize, int outputSize)
    {
        _inputSize = inputSize;
        _hiddenSize = hiddenSize;
        _outputSize = outputSize;
        _w1 = new float[inputSize, hiddenSize];
        _b1 = new float[hiddenSize];
        _w2 = new float[hiddenSize, outputSize];
        _b2 = new float[outputSize];
        XavierInit(_w1, inputSize, hiddenSize);
        XavierInit(_w2, hiddenSize, outputSize);
    }

    private static void XavierInit(float[,] weights, int fanIn, int fanOut)
    {
        var scale = (float)System.Math.Sqrt(2.0 / (fanIn + fanOut));
        for (int i = 0; i < weights.GetLength(0); i++)
            for (int j = 0; j < weights.GetLength(1); j++)
                weights[i, j] = (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * scale;
    }

    public float[] Predict(float[] input)
    {
        var hidden = new float[_hiddenSize];
        for (int j = 0; j < _hiddenSize; j++)
        {
            var sum = _b1[j];
            for (int i = 0; i < _inputSize; i++)
                sum += input[i] * _w1[i, j];
            hidden[j] = System.Math.Max(0, sum);
        }

        var output = new float[_outputSize];
        for (int k = 0; k < _outputSize; k++)
        {
            var sum = _b2[k];
            for (int j = 0; j < _hiddenSize; j++)
                sum += hidden[j] * _w2[j, k];
            output[k] = sum;
        }

        Softmax(output);
        return output;
    }

    private static void Softmax(float[] values)
    {
        var max = values[0];
        for (int i = 1; i < values.Length; i++)
            if (values[i] > max) max = values[i];

        var sum = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (float)System.Math.Exp(values[i] - max);
            sum += values[i];
        }
        for (int i = 0; i < values.Length; i++)
            values[i] /= sum;
    }

    public void Train(IReadOnlyList<(float[] Input, int Label)> examples, int epochs, float learningRate)
    {
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            foreach (var (input, label) in examples)
            {
                var hidden = new float[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                {
                    var sum = _b1[j];
                    for (int i = 0; i < _inputSize; i++)
                        sum += input[i] * _w1[i, j];
                    hidden[j] = System.Math.Max(0, sum);
                }

                var preSoftmax = new float[_outputSize];
                for (int k = 0; k < _outputSize; k++)
                {
                    var sum = _b2[k];
                    for (int j = 0; j < _hiddenSize; j++)
                        sum += hidden[j] * _w2[j, k];
                    preSoftmax[k] = sum;
                }

                var softmaxOut = (float[])preSoftmax.Clone();
                Softmax(softmaxOut);

                var dOutput = new float[_outputSize];
                for (int k = 0; k < _outputSize; k++)
                    dOutput[k] = softmaxOut[k] - (k == label ? 1 : 0);

                var dHidden = new float[_hiddenSize];
                for (int j = 0; j < _hiddenSize; j++)
                {
                    var sum = 0f;
                    for (int k = 0; k < _outputSize; k++)
                        sum += dOutput[k] * _w2[j, k];
                    dHidden[j] = sum * (hidden[j] > 0 ? 1 : 0);
                }

                for (int j = 0; j < _hiddenSize; j++)
                    for (int k = 0; k < _outputSize; k++)
                        _w2[j, k] -= learningRate * dOutput[k] * hidden[j];
                for (int k = 0; k < _outputSize; k++)
                    _b2[k] -= learningRate * dOutput[k];

                for (int i = 0; i < _inputSize; i++)
                    for (int j = 0; j < _hiddenSize; j++)
                        _w1[i, j] -= learningRate * dHidden[j] * input[i];
                for (int j = 0; j < _hiddenSize; j++)
                    _b1[j] -= learningRate * dHidden[j];
            }
        }
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var writer = new BinaryWriter(File.OpenWrite(path));
        writer.Write(_inputSize);
        writer.Write(_hiddenSize);
        writer.Write(_outputSize);

        for (int i = 0; i < _inputSize; i++)
            for (int j = 0; j < _hiddenSize; j++)
                writer.Write(_w1[i, j]);
        for (int j = 0; j < _hiddenSize; j++)
            writer.Write(_b1[j]);
        for (int j = 0; j < _hiddenSize; j++)
            for (int k = 0; k < _outputSize; k++)
                writer.Write(_w2[j, k]);
        for (int k = 0; k < _outputSize; k++)
            writer.Write(_b2[k]);
    }

    public static SimpleNeuralNet? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            var inputSize = reader.ReadInt32();
            var hiddenSize = reader.ReadInt32();
            var outputSize = reader.ReadInt32();
            var net = new SimpleNeuralNet(inputSize, hiddenSize, outputSize);
            for (int i = 0; i < inputSize; i++)
                for (int j = 0; j < hiddenSize; j++)
                    net._w1[i, j] = reader.ReadSingle();
            for (int j = 0; j < hiddenSize; j++)
                net._b1[j] = reader.ReadSingle();
            for (int j = 0; j < hiddenSize; j++)
                for (int k = 0; k < outputSize; k++)
                    net._w2[j, k] = reader.ReadSingle();
            for (int k = 0; k < outputSize; k++)
                net._b2[k] = reader.ReadSingle();
            return net;
        }
        catch
        {
            return null;
        }
    }
}
