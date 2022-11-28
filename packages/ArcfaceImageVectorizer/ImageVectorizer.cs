using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;


namespace ArcfaceImageVectorizer;

public class ImageVectorizer
{
    private const string _ONNX_ARCFACE_MODEL_RESOUCE = "ArcfaceImageVectorizer.arcfaceresnet100-8.onnx";

    public ImageVectorizer() {
        using var modelStream = typeof(ImageVectorizer).Assembly.GetManifestResourceStream(_ONNX_ARCFACE_MODEL_RESOUCE);
        using var memoryStream = new MemoryStream();
        modelStream.CopyTo(memoryStream);

        var sessionOptions = new SessionOptions();
        sessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL;
        this._inferenceSession = new InferenceSession(memoryStream.ToArray(), sessionOptions);
    }

    ~ImageVectorizer()
    {
        this._inferenceSession.Dispose();
    }

    public Task<float[]> StartCalculating(Image<Rgb24> image, CancellationTokenSource cancellationToken = null)
    {
        Func<float[]> currentTask = 
            () =>
            {
                var input =
                    new List<NamedOnnxValue> {NamedOnnxValue.CreateFromTensor("data", ImageToTensor(image))};
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession.Run(input);
                return results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray();
            };

        if (cancellationToken is null)
        {
            return Task<float[]>.Run(currentTask);
        }
        return Task<float[]>.Run(currentTask, cancellationToken.Token);
    }

    private static DenseTensor<float> ImageToTensor(Image<Rgb24> image)
    {
        var width = image.Width;
        var height = image.Height;
        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });

        image.ProcessPixelRows(pa => 
        {
            for (int y = 0; y < height; y++)
            {           
                Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    tensor[0, 0, y, x] = pixelSpan[x].R;
                    tensor[0, 1, y, x] = pixelSpan[x].G;
                    tensor[0, 2, y, x] = pixelSpan[x].B;
                }
            }
        });
        
        return tensor;
    }

    private InferenceSession _inferenceSession;
}
