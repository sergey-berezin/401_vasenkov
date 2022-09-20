using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;

using image_functions_performer;
using arcface_async_vectorizer;

const int IMAGES_COUNT = 2;
const int TEST_SAMPLE_SIZE = 10;

// init ML
var imageFunctionsPerformer = new ImageFunctionsPerformer();

// Reading images from files
var images = new List<Image<Rgb24>>();
for (int i = 1; i <= IMAGES_COUNT; ++i)
{
    images.Add(Image.Load<Rgb24>($"faces/{i}.png"));
}


// Synchronous test

var distances1 = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
var similarity1 = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

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
            distances1[index] = imageFunctionsPerformer.GetDistance(images[i], images[j]);
            similarity1[index] = imageFunctionsPerformer.GetSimilarity(images[i], images[j]);
        }
    }
}

stopwatch.Stop();

Console.WriteLine($"Time for synchronous requests to ML {stopwatch.ElapsedMilliseconds} ms");


// Asynchronous test

// Task holders
var distance_futures = new Task<float>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
var similarity_futures = new Task<float>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
// results
var distances2 = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
var similarity2 = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

// start timer
stopwatch = new Stopwatch();
stopwatch.Start();

for (int k = 0; k < TEST_SAMPLE_SIZE; ++k)
{
    for (int i = 0; i < IMAGES_COUNT; ++i)
    {
        for (int j = 0; j < IMAGES_COUNT; ++j)
        {
            var index = k * IMAGES_COUNT * IMAGES_COUNT + i * IMAGES_COUNT + j;
            distance_futures[index] = imageFunctionsPerformer.GetDistanceAsync(images[i], images[j]).Task;
            similarity_futures[index] = imageFunctionsPerformer.GetSimilarityAsync(images[i], images[j]).Task;
        }
    }
}

for (int i = 0; i < distance_futures.Length; ++i)
{
    distances2[i] = distance_futures[i].Result;
    similarity2[i] = similarity_futures[i].Result;
}

stopwatch.Stop();

Console.WriteLine($"Time for asynchronous requests to ML {stopwatch.ElapsedMilliseconds} ms");

// Asynchronous test with cancellation of distance calculation
// Task holders
var distance_futures_for_cancellation = new Task<float>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
var similarity_futures_for_cancellation = new Task<float>[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];
var similarity3 = new float[IMAGES_COUNT * IMAGES_COUNT * TEST_SAMPLE_SIZE];

// start timer
stopwatch = new Stopwatch();
stopwatch.Start();

int first_cancelled_task_index = -1;

for (int k = 0; k < TEST_SAMPLE_SIZE; ++k)
{
    for (int i = 0; i < IMAGES_COUNT; ++i)
    {
        for (int j = 0; j < IMAGES_COUNT; ++j)
        {
            var index = k * IMAGES_COUNT * IMAGES_COUNT + i * IMAGES_COUNT + j;
            var distance_values = imageFunctionsPerformer.GetDistanceAsync(images[i], images[j]);
            distance_futures_for_cancellation[index] = distance_values.Task;
            if (imageFunctionsPerformer.CancelCalculation(distance_values.CancellationTokenId) &&
                first_cancelled_task_index == -1)
            {
                first_cancelled_task_index = index;
            }
            var similarity_values = imageFunctionsPerformer.GetSimilarityAsync(images[i], images[j]);
            similarity_futures_for_cancellation[index] = similarity_values.Task;
        }
    }
}

for (int i = 0; i < similarity_futures_for_cancellation.Length; ++i)
{
    similarity3[i] = similarity_futures_for_cancellation[i].Result;
}

stopwatch.Stop();

Console.WriteLine($"Time for asynchronous requests to ML with half cancelled tasks {stopwatch.ElapsedMilliseconds} ms");

// Calculation results

Console.WriteLine();
Console.WriteLine("Synchronous distances:");
for (int i = 0; i < IMAGES_COUNT * IMAGES_COUNT; ++i) {
    Console.WriteLine($"{i / IMAGES_COUNT + 1}.png - {i % IMAGES_COUNT + 1}.png: {distances1[i]}");
}

Console.WriteLine();
Console.WriteLine("Synchronous similarity:");
for (int i = 0; i < IMAGES_COUNT * IMAGES_COUNT; ++i) {
    Console.WriteLine($"{i / IMAGES_COUNT + 1}.png - {i % IMAGES_COUNT + 1}.png: {similarity1[i]}");
}

Console.WriteLine();
Console.WriteLine("Asynchronous distances:");
for (int i = 0; i < IMAGES_COUNT * IMAGES_COUNT; ++i) {
    Console.WriteLine($"{i / IMAGES_COUNT + 1}.png - {i % IMAGES_COUNT + 1}.png: {distances2[i]}");
}

Console.WriteLine();
Console.WriteLine("Asynchronous similarity:");
for (int i = 0; i < IMAGES_COUNT * IMAGES_COUNT; ++i) {
    Console.WriteLine($"{i / IMAGES_COUNT + 1}.png - {i % IMAGES_COUNT + 1}.png: {similarity2[i]}");
}

Console.WriteLine();
Console.WriteLine("Asynchronous similarity after distances cancellation:");
for (int i = 0; i < IMAGES_COUNT * IMAGES_COUNT; ++i) {
    Console.WriteLine($"{i / IMAGES_COUNT + 1}.png - {i % IMAGES_COUNT + 1}.png: {similarity3[i]}");
}
