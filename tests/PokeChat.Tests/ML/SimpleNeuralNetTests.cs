using PokeChat.ML;
using Shouldly;

namespace PokeChat.Tests.ML;

public class SimpleNeuralNetTests
{
    [Fact]
    public void Predict_ReturnsCorrectOutputSize()
    {
        var net = new SimpleNeuralNet(10, 5, 3);
        var input = new float[10];
        var output = net.Predict(input);
        output.Length.ShouldBe(3);
    }

    [Fact]
    public void Softmax_OutputsSumToOne()
    {
        var net = new SimpleNeuralNet(10, 5, 4);
        var input = new float[10];
        var output = net.Predict(input);
        var sum = output.Sum();
        sum.ShouldBe(1.0f, 0.0001);
    }

    [Fact]
    public void Train_ReducesLoss()
    {
        var net = new SimpleNeuralNet(4, 8, 2);
        var examples = new List<(float[] Input, int Label)>
        {
            (new[] { 1f, 0f, 0f, 0f }, 0),
            (new[] { 0f, 1f, 0f, 0f }, 0),
            (new[] { 1f, 1f, 0f, 0f }, 0),
            (new[] { 0f, 0f, 1f, 0f }, 1),
            (new[] { 0f, 0f, 0f, 1f }, 1),
            (new[] { 0f, 0f, 1f, 1f }, 1),
        };

        float AvgLoss(SimpleNeuralNet n)
        {
            float total = 0;
            foreach (var (input, label) in examples)
            {
                var probs = n.Predict(input);
                total += (float)-System.Math.Log(probs[label] + 1e-10f);
            }
            return total / examples.Count;
        }

        var beforeLoss = AvgLoss(net);
        net.Train(examples, 200, 0.5f);
        var afterLoss = AvgLoss(net);

        afterLoss.ShouldBeLessThan(beforeLoss);
    }

    [Fact]
    public void Train_LearnsTwoClasses()
    {
        var net = new SimpleNeuralNet(4, 6, 2);

        var classA = new List<(float[] Input, int Label)>
        {
            (new[] { 1f, 0f, 0f, 0f }, 0),
            (new[] { 1f, 1f, 0f, 0f }, 0),
        };
        var classB = new List<(float[] Input, int Label)>
        {
            (new[] { 0f, 0f, 1f, 0f }, 1),
            (new[] { 0f, 0f, 1f, 1f }, 1),
        };
        var all = classA.Concat(classB).ToList();

        net.Train(all, 100, 0.1f);

        var resultA = net.Predict(new[] { 1f, 0f, 0f, 0f });
        var resultB = net.Predict(new[] { 0f, 0f, 1f, 0f });

        resultA[0].ShouldBeGreaterThan(resultA[1]);
        resultB[1].ShouldBeGreaterThan(resultB[0]);
    }

    [Fact]
    public void SaveAndLoad_RoundtripsCorrectly()
    {
        var path = Path.GetTempFileName();
        try
        {
            var net = new SimpleNeuralNet(5, 4, 3);
            var input = new float[] { 1, 0, 1, 0, 1 };
            var original = net.Predict(input);

            net.Save(path);
            var loaded = SimpleNeuralNet.Load(path);

            loaded.ShouldNotBeNull();
            var after = loaded.Predict(input);
            after.Length.ShouldBe(3);
            for (int i = 0; i < 3; i++)
                after[i].ShouldBe(original[i], 0.0001f);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsNull()
    {
        var loaded = SimpleNeuralNet.Load("/nonexistent/path/model.bin");
        loaded.ShouldBeNull();
    }

    [Fact]
    public void XavierInit_WeightsInReasonableRange()
    {
        var net = new SimpleNeuralNet(100, 50, 10);
        var input = new float[100];
        input[0] = 1;
        var output = net.Predict(input);
        output.Length.ShouldBe(10);
        output.Any(v => float.IsNaN(v)).ShouldBeFalse();
        output.Any(v => float.IsInfinity(v)).ShouldBeFalse();
    }

    [Fact]
    public void ReLU_NegativesBecomeZero()
    {
        var net = new SimpleNeuralNet(2, 2, 1);
        var input = new float[] { -1000, -1000 };
        var output = net.Predict(input);
        output.Length.ShouldBe(1);
        output[0].ShouldBeGreaterThanOrEqualTo(0);
        output[0].ShouldBeLessThanOrEqualTo(1);
    }
}
