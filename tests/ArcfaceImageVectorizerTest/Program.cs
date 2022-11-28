using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Text.Json;

using ArcfaceImageFunctionsPerformer;
using ArcfaceImageVectorizer;


const int IMAGES_COUNT = 2;
const int TEST_SAMPLE_SIZE = 10;

// init ML
var ImageFunctionsPerformer = new ImageFunctionsPerformer();
var ImageVectorizer = new ImageVectorizer();


float GetDistance(Image<Rgb24> a, Image<Rgb24> b)
{
    var v1 = ImageVectorizer.StartCalculating(a).Result;
    var v2 = ImageVectorizer.StartCalculating(b).Result;
    return ImageFunctionsPerformer.GetDistance(v1, v2);
}

float GetSimilarity(Image<Rgb24> a, Image<Rgb24> b)
{
    var v1 = ImageVectorizer.StartCalculating(a).Result;
    var v2 = ImageVectorizer.StartCalculating(b).Result;
    return ImageFunctionsPerformer.GetSimilarity(v1, v2);
}

// Reading images from files
var images = new List<Image<Rgb24>>();
for (int i = 1; i <= IMAGES_COUNT; ++i)
{
    images.Add(Image.Load<Rgb24>($"faces/{i}.png"));
}

// Synchronous test function
(float[] distances, float[] similarities) SynchronousTest()
{
    var distances = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var similarities = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

    // start timer
    var stopwatch = new Stopwatch();
    stopwatch.Start();

    for (int k = 0; k < TEST_SAMPLE_SIZE; ++k)
    {
        for (int i = 0; i < IMAGES_COUNT; ++i)
        {
            for (int j = 0; j < IMAGES_COUNT; ++j)
            {
                var index = k * IMAGES_COUNT * IMAGES_COUNT + i * IMAGES_COUNT + j;
                distances[index] = GetDistance(images[i], images[j]);
                similarities[index] = GetSimilarity(images[i], images[j]);
            }
        }
    }

    stopwatch.Stop();

    Console.WriteLine($"Time for synchronous requests to ML {stopwatch.ElapsedMilliseconds} ms");
    
    return (distances, similarities);
}

// Async test function
(float[] distances, float[] similarities) AsynchronousTest()
{
    // Task holders
    var imageVectors1 = new Task<float[]>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var imageVectors2 = new Task<float[]>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var distanceAndSimilarityFutures = new Task<float[]>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

    var distances = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var similarities = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

    // start timer
    var stopwatch = new Stopwatch();
    stopwatch.Start();

    for (int k = 0; k < TEST_SAMPLE_SIZE; ++k)
    {
        for (int i = 0; i < IMAGES_COUNT; ++i)
        {
            for (int j = 0; j < IMAGES_COUNT; ++j)
            {
                var index = k * IMAGES_COUNT * IMAGES_COUNT + i * IMAGES_COUNT + j;
                imageVectors1[index] = ImageVectorizer.StartCalculating(images[i]);
                imageVectors2[index] = ImageVectorizer.StartCalculating(images[j]);
            }
        }
    }

    for (int i = 0; i < distanceAndSimilarityFutures.Length; ++i)
    {
        distanceAndSimilarityFutures[i] = 
            ImageFunctionsPerformer.GetDistanceAndSimilarityByVectors(
                imageVectors1[i].Result, imageVectors2[i].Result);
    }

    for (int i = 0; i < distanceAndSimilarityFutures.Length; ++i)
    {
        var result = distanceAndSimilarityFutures[i].Result;
        distances[i] = result[0];
        similarities[i] = result[1];
    }

    stopwatch.Stop();

    Console.WriteLine($"Time for asynchronous requests to ML {stopwatch.ElapsedMilliseconds} ms");

    return (distances, similarities);
}


// Async with cancellation test function
void AsynchronousTestWithCancellation()
{
    // Task holders
    var imageVectors1 = new Task<float[]>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var imageVectors2 = new Task<float[]>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var distanceAndSimilarityFutures = new Task<float[]>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

    var distances = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
    var similarities = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

    // start timer
    var stopwatch = new Stopwatch();
    stopwatch.Start();

    var cancellationToken = new CancellationTokenSource();

    for (int k = 0; k < TEST_SAMPLE_SIZE; ++k)
    {
        for (int i = 0; i < IMAGES_COUNT; ++i)
        {
            for (int j = 0; j < IMAGES_COUNT; ++j)
            {
                var index = k * IMAGES_COUNT * IMAGES_COUNT + i * IMAGES_COUNT + j;
                imageVectors1[index] = ImageVectorizer.StartCalculating(images[i], cancellationToken);
                imageVectors2[index] = ImageVectorizer.StartCalculating(images[j], cancellationToken);
            }
        }
    }

    cancellationToken.Cancel();

    try {
        for (int i = 0; i < distanceAndSimilarityFutures.Length; ++i)
        {
            distanceAndSimilarityFutures[i] = 
                ImageFunctionsPerformer.GetDistanceAndSimilarityByVectors(
                    imageVectors1[i].Result, imageVectors2[i].Result);
        }
    }
    catch
    {
        Console.WriteLine(
            "Some exception occured during access to cancelled tasks results!" +
            "(Was expected due to tasks cancellation)"
        );
    }

    stopwatch.Stop();

    Console.WriteLine(
        $"Time for asynchronous requests with cancellation to ML {stopwatch.ElapsedMilliseconds} ms");
}

void WriteResults(string msg, float[] results)
{
    Console.WriteLine();
    Console.WriteLine(msg);
    for (int i = 0; i < IMAGES_COUNT * IMAGES_COUNT; ++i) {
        Console.WriteLine($"{i / IMAGES_COUNT + 1}.png - {i % IMAGES_COUNT + 1}.png: {results[i]}");
    }
}


// Synchronous test
var (distances1, similarities1) = SynchronousTest();

// Asynchronous test
var (distances2, similarities2) = AsynchronousTest();

// Asynchronous test with cancellation
AsynchronousTestWithCancellation();

WriteResults("Synchronous distances:", distances1);
WriteResults("Synchronous similarities:", similarities1);
WriteResults("Asynchronous distances:", distances2);
WriteResults("Asynchronous similarities:", similarities2);
