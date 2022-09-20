using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;


namespace arcface_async_vectorizer;

public class ImageVectorizer
{
    private static readonly string _ONNX_ARCFACE_MODEL_RESOUCE = "arcface_async_vectorizer.arcfaceresnet100-8.onnx";

    public ImageVectorizer() {
        using var modelStream = typeof(ImageVectorizer).Assembly.GetManifestResourceStream(_ONNX_ARCFACE_MODEL_RESOUCE);
        using var memoryStream = new MemoryStream();
        modelStream.CopyTo(memoryStream);

        var sessionOptions = new SessionOptions();
        sessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL;
        this._inferenceSession = new InferenceSession(memoryStream.ToArray(), sessionOptions);
        this._calculationsHolder = new Dictionary<string, Task<float[]>>();
        this._calculationsHolderMutex = new Mutex();
    }

    ~ImageVectorizer()
    {
        this._inferenceSession.Dispose();
    }

    public string StartCalculating(Image<Rgb24> image)
    {
        var sessionId = Guid.NewGuid().ToString();

        Func<float[]> current_task = 
            () =>
            {
                var input =
                    new List<NamedOnnxValue> {NamedOnnxValue.CreateFromTensor("data", ImageToTensor(image))};
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession.Run(input);
                return results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray();
            };

        var task = Task<float[]>.Run(current_task);
    
        _calculationsHolderMutex.WaitOne();
        _calculationsHolder[sessionId] = task;
        _calculationsHolderMutex.ReleaseMutex();

        return sessionId;
    }

    public float[] GetResultBySessionId(string sessionId)
    {
        _calculationsHolderMutex.WaitOne();
        if (!_calculationsHolder.ContainsKey(sessionId))
        {
            throw new exceptions.NotFound("No session id was found");
        }

        var task = _calculationsHolder[sessionId];
        
        _calculationsHolder.Remove(sessionId);
        _calculationsHolderMutex.ReleaseMutex();

        return task.Result;
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
    private Dictionary<string, Task<float[]>> _calculationsHolder;
    private Mutex _calculationsHolderMutex;
}
