using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using arcface_async_vectorizer;


namespace image_functions_performer;

public class ImageFunctionsPerformer
{
    public ImageFunctionsPerformer() {
        this._imageVectorizer = new ImageVectorizer();
        this._cancellationTokens = new Dictionary<string, CancellationTokenSource>();
        this._cancellationTokensMutex = new Mutex();
    }
    
    public (Task<float> Task, string CancellationTokenId) GetDistanceAsync(Image<Rgb24> image1, Image<Rgb24> image2)
    {
        return CommonAsyncTaskLauncher(image1, image2, Distance);
    }

    public (Task<float> Task, string CancellationTokenId) GetSimilarityAsync(Image<Rgb24> image1, Image<Rgb24> image2)
    {
        return CommonAsyncTaskLauncher(image1, image2, Similarity);
    }

    public float GetDistance(Image<Rgb24> image1, Image<Rgb24> image2)
    {
        return CommonTaskLauncher(image1, image2, Distance);
    }

    public float GetSimilarity(Image<Rgb24> image1, Image<Rgb24> image2)
    {
        return CommonTaskLauncher(image1, image2, Similarity);
    }

    public bool CancelCalculation(string cancellationTokenId)
    {
        _cancellationTokensMutex.WaitOne();

        if (!_cancellationTokens.ContainsKey(cancellationTokenId))
        {
            _cancellationTokensMutex.ReleaseMutex();
            return false;
        }

        _cancellationTokens[cancellationTokenId].Cancel();
        _cancellationTokens.Remove(cancellationTokenId);
        _cancellationTokensMutex.ReleaseMutex();
        return true;
    }

    private delegate float CalculationCallback(float[] v1, float[] v2);

    private (Task<float> Task, string CancellationTokenUuid) CommonAsyncTaskLauncher(
        Image<Rgb24> image1, Image<Rgb24> image2, CalculationCallback callback
    ) {
        var firstImageSessionId = _imageVectorizer.StartCalculating(image1);
        var secondImageSessionId = _imageVectorizer.StartCalculating(image2);
        var cancellationTokenId = Guid.NewGuid().ToString();
        var tokenSource = new CancellationTokenSource();

        _cancellationTokensMutex.WaitOne();
        _cancellationTokens[cancellationTokenId] = tokenSource;
        _cancellationTokensMutex.ReleaseMutex();

        Func<float> current_task =
            () =>
            {
                var firstVector = _imageVectorizer.GetResultBySessionId(firstImageSessionId);
                var secondVector = _imageVectorizer.GetResultBySessionId(secondImageSessionId);
                return callback(firstVector, secondVector);
            };

        return (Task<float>.Run(current_task, tokenSource.Token), cancellationTokenId);
    }

    private float CommonTaskLauncher(Image<Rgb24> image1, Image<Rgb24> image2, CalculationCallback callback) {
        var firstImageSessionId = _imageVectorizer.StartCalculating(image1);
        var firstVector = _imageVectorizer.GetResultBySessionId(firstImageSessionId);

        var secondImageSessionId = _imageVectorizer.StartCalculating(image2);
        var secondVector = _imageVectorizer.GetResultBySessionId(secondImageSessionId);

        return callback(firstVector, secondVector);
    }

    private float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x*x).Sum());

    private float Distance(float[] v1, float[] v2) => Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());

    private float Similarity(float[] v1, float[] v2) => v1.Zip(v2).Select(p => p.First * p.Second).Sum();

    private ImageVectorizer _imageVectorizer;
    private Dictionary<string, CancellationTokenSource> _cancellationTokens;
    private Mutex _cancellationTokensMutex;
}