using PokeChat.ML;
using Shouldly;

namespace PokeChat.Tests.ML;

public class IntentClassifierTests
{
    [Fact]
    public void Classify_ReturnsNull_WhenNotReady()
    {
        var classifier = new IntentClassifier();
        classifier.IsReady.ShouldBeFalse();
        var result = classifier.Classify("hello there");
        result.ShouldBeNull();
    }

    [Fact]
    public void TrainAndClassify_ReturnsCategory()
    {
        var classifier = new IntentClassifier();
        var examples = new List<(string Input, string Category)>
        {
            ("hello", "greeting"),
            ("hi there", "greeting"),
            ("hey how are you", "greeting"),
            ("good morning", "greeting"),
            ("good afternoon", "greeting"),
            ("good evening", "greeting"),
            ("howdy partner", "greeting"),
            ("whats up", "greeting"),
            ("tell me a story", "story_request"),
            ("make up a story", "story_request"),
            ("tell me a tale", "story_request"),
            ("i want a story", "story_request"),
            ("can you tell me a story", "story_request"),
            ("tell me something interesting", "story_request"),
            ("weave me a story", "story_request"),
            ("i like pizza", "preference_statement"),
            ("i love cats", "preference_statement"),
            ("i enjoy running", "preference_statement"),
            ("i hate spiders", "preference_statement"),
            ("i am fond of music", "preference_statement"),
            ("i adore chocolate", "preference_statement"),
            ("i dislike rain", "preference_statement"),
            ("goodbye", "farewell"),
            ("see you later", "farewell"),
            ("i have to go", "farewell"),
            ("got to go", "farewell"),
            ("talk to you later", "farewell"),
            ("catch you later", "farewell"),
            ("see you soon", "farewell"),
            ("what is 2 plus 2", "math_query"),
            ("calculate 5 plus 3", "math_query"),
            ("what is 7 times 8", "math_query"),
            ("compute 10 minus 4", "math_query"),
            ("how much is 3 plus 5", "math_query"),
            ("tell me a joke", "joke_request"),
            ("make me laugh", "joke_request"),
            ("crack a joke", "joke_request"),
            ("tell me something funny", "joke_request"),
            ("do you know any jokes", "joke_request"),
        };

        classifier.Train(examples);
        classifier.IsReady.ShouldBeTrue();

        var greeting = classifier.Classify("hey there");
        greeting.ShouldBe("greeting");

        var story = classifier.Classify("tell me a story please");
        story.ShouldBe("story_request");

        var preference = classifier.Classify("i enjoy reading books");
        preference.ShouldBe("preference_statement");

        var farewell = classifier.Classify("goodbye for now");
        farewell.ShouldBe("farewell");
    }

    [Fact]
    public void BuildVocab_ProducesCorrectSize()
    {
        var classifier = new IntentClassifier();
        var texts = new[] { "hello world", "hello there", "world of code", "a b c d e f g h i j" };
        classifier.BuildVocab(texts);

        classifier.VocabSize.ShouldBeGreaterThan(0);
        classifier.VocabSize.ShouldBeLessThanOrEqualTo(2000);
    }

    [Fact]
    public void Vectorise_ProducesCorrectDimensions()
    {
        var classifier = new IntentClassifier();
        var texts = new[] { "hello world", "hello there", "world of code" };
        classifier.BuildVocab(texts);

        var vec = classifier.Vectorise("hello world");
        vec.Length.ShouldBe(classifier.VocabSize);
        vec.Any(v => v > 0).ShouldBeTrue();
    }

    [Fact]
    public void Classify_LowConfidence_ReturnsNull()
    {
        var classifier = new IntentClassifier();
        var examples = new List<(string Input, string Category)>
        {
            ("hello hi hey good morning", "greeting"),
            ("goodbye farewell see you", "farewell"),
        };

        classifier.Train(examples);

        var result = classifier.Classify("xylophone quantum");
        result.ShouldBeNull();
    }

    [Fact]
    public void Train_WithEmptyExamples_DoesNotCrash()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string Input, string Category)>());
        classifier.IsReady.ShouldBeFalse();
    }
}
