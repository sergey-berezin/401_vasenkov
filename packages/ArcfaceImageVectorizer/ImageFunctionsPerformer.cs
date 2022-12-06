using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace ArcfaceImageFunctionsPerformer;

public class ImageFunctionsPerformer
{
    private const float _EPS = 1e-7F;

    public ImageFunctionsPerformer() {}

    public static Task<float[]> GetDistanceAndSimilarityByVectors(
        float[] a, float[] b, CancellationTokenSource cancellationToken = null)
    {
        var tasks = new List<Task<float>>();
        Func<float> getDistance = () => { return GetDistance(a, b); };
        Func<float> getSimilarity = () => { return GetSimilarity(a, b); };

        if (cancellationToken is null)
        {
            tasks.Add(Task<float>.Run(getDistance));
            tasks.Add(Task<float>.Run(getSimilarity));
        }
        else
        {
            tasks.Add(Task<float>.Run(getDistance, cancellationToken.Token));
            tasks.Add(Task<float>.Run(getSimilarity, cancellationToken.Token));
        }

        return Task.WhenAll(tasks.ToArray());
    }

    public static float GetLength(float[] v) => (float)Math.Sqrt(v.Select(x => x*x).Sum());

    public static float GetDistance(float[] v1, float[] v2) => GetLength(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());

    private static float GetDotProduction(float[] v1, float[] v2) => v1.Zip(v2).Select(p => p.First * p.Second).Sum();

    public static float GetSimilarity(float[] v1, float[] v2)
    {
        var length1 = GetLength(v1);
        var length2 = GetLength(v2);
        var dotProduction = GetDotProduction(v1, v2);
        if (Math.Abs(length1) < _EPS || Math.Abs(length2) < _EPS)
        {
            if (Math.Abs(length1 - length2) < _EPS) return 1;
            return 0;
        }
        return dotProduction / length1 / length2;
    }
}