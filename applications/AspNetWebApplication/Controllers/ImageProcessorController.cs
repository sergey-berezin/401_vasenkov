using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

using Models;
using Services;
using EmbeddedResources;
using AspNetWebApplication;
using ArcfaceImageVectorizer;
using ArcfaceImageFunctionsPerformer;

namespace AspNetWebApplication.Controllers;

public class ImagesConverter
{
    private int Width { get; }
    private int Height { get; }

    public ImagesConverter(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public byte[] Convert(byte[] rawImage)
    {
        var image = SixLabors.ImageSharp.Image.Load<Rgb24>(rawImage);
        image.Mutate<Rgb24>(x => x.Resize(Width, Height)); 
        byte[] rawImageWithCommonShape;

        using (var memoryStream = new MemoryStream())
        {
            image.Save(memoryStream, new PngEncoder());
            rawImageWithCommonShape = memoryStream.ToArray();
        }

        return rawImageWithCommonShape;
    }

    public Image<Rgb24> ConvertToImage(byte[] rawImage)
    {
        return SixLabors.ImageSharp.Image.Load<Rgb24>(rawImage);
    }
}

public class BodyWithImageId
{
    [JsonPropertyName("image_id")]
    public string ImageId { get; set; }

    public BodyWithImageId(string imageId)
    {
        ImageId = imageId;
    }
}

public class Error
{
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    public Error() {}
}

public class ImageIdsPair
{
    [JsonPropertyName("first_image_id")]
    public string FirstImageId { get; set; }

    [JsonPropertyName("second_image_id")]
    public string SecondImageId { get; set; }

    public ImageIdsPair() {}
}

public class DistanceAndSimilarity
{
    [JsonPropertyName("distance")]
    public float? Distance { get; set; }

    [JsonPropertyName("similarity")]
    public float? Similarity { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    public DistanceAndSimilarity(
        float progress, float? distance = null, float? similarity = null)
    {
        Progress = progress;
        Distance = distance;
        Similarity = similarity;
    }
}

public class ImageIdsByRowAndColumn
{
    [JsonPropertyName("row")]
    public string[] Row { get; set; }

    [JsonPropertyName("column")]
    public string[] Column { get; set; }

    public ImageIdsByRowAndColumn(string[] rowImageIds, string[] columnImageIds)
    {
        Row = rowImageIds;
        Column = columnImageIds;
    }
}

[ApiController]
public class ImageProcessorController : ControllerBase
{
    [HttpGet("")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult GetMainPage()
    {
        return base.Content(MainPage, "text/html");
    }

    private long GetImageHash(byte[] rawImage)
    {
        long result = 0;
        foreach (var currentByte in rawImage)
        {
            result *= 257;
            result += (long) currentByte;
        }
        return result;
    }

    private bool AreImagesEqual(byte[] firstImage, byte[] secondImage)
    {
        if (firstImage.Length != secondImage.Length)
        {
            return false;
        }
        for (int i = 0; i < firstImage.Length; ++i)
        {
            if (firstImage[i] != secondImage[i])
            {
                return false;
            }
        }
        return true;
    }

    [HttpPost("images/save")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult SaveImage([FromQuery] string table_element_type)
    {
        byte[] image;
        using (var memoryStream = new MemoryStream())
        {
            HttpContext.Request.Body.CopyToAsync(memoryStream).Wait();
            image = memoryStream.ToArray();
        }

        if (table_element_type != "ROW" && table_element_type != "COLUMN")
        {
            return BadRequest();
        }

        var rawImage = ImagesConverter.Convert(image);
        var imageHash = GetImageHash(rawImage);

        var imageInfos = Db.ImageInfos.Where(image => image.ImageHash == imageHash).ToList();
        foreach (var imageInfo in imageInfos)
        {
            if (AreImagesEqual(imageInfo.Image, rawImage))
            {
                if (imageInfo.Type != "BOTH" && imageInfo.Type != table_element_type)
                {
                    imageInfo.Type = "BOTH";
                    Db.ImageInfos.Update(imageInfo);
                    Db.SaveChanges();
                }
                return Ok(new BodyWithImageId(imageInfo.ImageId));
            }
        }

        var newImageInfo = new ImageInfo(
            rawImage, table_element_type, Guid.NewGuid().ToString(), imageHash);

        Db.ImageInfos.Add(newImageInfo);
        Db.SaveChanges();
        return Ok(new BodyWithImageId(newImageInfo.ImageId));
    }

    [HttpGet("images/{imageId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetImage(string imageId)
    {
        var images = Db.ImageInfos.Where(image => image.ImageId == imageId).ToList();
        if (images.Count == 1) {
            return base.File(images[0].Image, "image/*");
        }
        if (images.Count == 0) {
            return NotFound();
        }
        throw new Exception($"Count of images with image_id({imageId}) = {images.Count}");
    }

    private void StartVectorization(string imageId, byte[] image) {
        var vtm = VectorizationTaskManager;

        lock (vtm.VectorizationTaskByImageId)
        lock (vtm.CancellationTokenSourcesByImageId)
        {
            if (vtm.VectorizationTaskByImageId.ContainsKey(imageId))
            {
                var task = vtm.VectorizationTaskByImageId[imageId];
                if (task.Status != TaskStatus.Faulted && task.Status != TaskStatus.Canceled)
                {
                    return;
                }
            }

            lock (vtm.StartVectorizationTimestamp)
            {
                vtm.StartVectorizationTimestamp[imageId] = new Stopwatch();
                vtm.StartVectorizationTimestamp[imageId].Start();
            }

            if (vtm.CancellationTokenSourcesByImageId.ContainsKey(imageId))
            {
                vtm.CancellationTokenSourcesByImageId[imageId].Dispose();
            }
            vtm.CancellationTokenSourcesByImageId[imageId] = new CancellationTokenSource();
            vtm.VectorizationTaskByImageId[imageId] = vtm.ImageVectorizer.StartCalculating(
                ImagesConverter.ConvertToImage(image),
                vtm.CancellationTokenSourcesByImageId[imageId]);
        }
    }

    [HttpPost("images/start_calculation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public StatusCodeResult StartCalculation([FromBody] ImageIdsPair imageIdsPair)
    {
        var firstImageInfo = Db.ImageInfos.Where(
            imageInfo => imageInfo.ImageId == imageIdsPair.FirstImageId).ToList();
        var secondImageInfo = Db.ImageInfos.Where(
            imageInfo => imageInfo.ImageId == imageIdsPair.SecondImageId).ToList();  
        if (firstImageInfo.Count == 0 || secondImageInfo.Count == 0) {
            return NotFound();
        }
        
        if (firstImageInfo[0].Embeddings is null)
        {
            StartVectorization(imageIdsPair.FirstImageId, firstImageInfo[0].Image);
        }

        if (secondImageInfo[0].Embeddings is null)
        {
            StartVectorization(imageIdsPair.SecondImageId, secondImageInfo[0].Image);
        }
        return Ok();
    }

    private float GetVectorizationProgress(ImageInfo imageInfo)
    {
        var imageId = imageInfo.ImageId;
        var vtm = VectorizationTaskManager;

        lock (vtm.VectorizationTaskByImageId)
        lock (vtm.CancellationTokenSourcesByImageId)
        lock (vtm.StartVectorizationTimestamp)
        {
            if (!vtm.VectorizationTaskByImageId.ContainsKey(imageId))
            {
                return 1f;
            }

            var task = vtm.VectorizationTaskByImageId[imageId];
            if (task.Status == TaskStatus.RanToCompletion)
            {
                if (vtm.StartVectorizationTimestamp.ContainsKey(imageId))
                {
                    Stopwatch stopwatch;
                    vtm.StartVectorizationTimestamp.TryRemove(imageId, out stopwatch);
                    stopwatch.Stop();
                    lock (vtm.ExecutionTimesQueue)
                    {
                        vtm.ExecutionTimesQueue.Push(stopwatch.ElapsedMilliseconds);
                    }
                }
                imageInfo.Embeddings = task.Result;
                Db.ImageInfos.Update(imageInfo);
                Console.WriteLine($"!!! {imageInfo.Embeddings}");
                Db.SaveChanges();
                CancellationTokenSource token;
                vtm.VectorizationTaskByImageId.TryRemove(imageId, out task);
                vtm.CancellationTokenSourcesByImageId.TryRemove(imageId, out token);
                token.Dispose();
                return 1;
            }

            if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Faulted)
            {
                if (vtm.StartVectorizationTimestamp.ContainsKey(imageId))
                {
                    Stopwatch stopwatch;
                    vtm.StartVectorizationTimestamp.TryRemove(imageId, out stopwatch);
                }
                return 0;
            }

            var elapsedMilliseconds = vtm.StartVectorizationTimestamp[imageId].ElapsedMilliseconds;
            var meanExecutionTime = vtm.ExecutionTimesQueue.MeanValue;
            return (new [] { (float) elapsedMilliseconds / meanExecutionTime, 1f}).Min();
        }
    }

    [HttpPost("images/compare")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult CompareImages([FromBody] ImageIdsPair imageIdsPair)
    {
        var firstImageInfo = Db.ImageInfos.Where(
            imageInfo => imageInfo.ImageId == imageIdsPair.FirstImageId).ToList();
        var secondImageInfo = Db.ImageInfos.Where(
            imageInfo => imageInfo.ImageId == imageIdsPair.SecondImageId).ToList();  
        if (firstImageInfo.Count == 0 || secondImageInfo.Count == 0) {
            return NotFound();
        }

        bool IsDistanceAndSimilarityCalculationEnabled = true;

        float firstProgress = 1;
        var firstEmbeddings = firstImageInfo[0].Embeddings;
        if (firstEmbeddings is null)
        {
            firstProgress = GetVectorizationProgress(firstImageInfo[0]);
            IsDistanceAndSimilarityCalculationEnabled = false;
        }

        float secondProgress = 1;
        var secondEmbeddings = secondImageInfo[0].Embeddings;
        if (secondEmbeddings is null)
        {
            secondProgress = GetVectorizationProgress(secondImageInfo[0]);
            IsDistanceAndSimilarityCalculationEnabled = false;
        }

        if (!IsDistanceAndSimilarityCalculationEnabled)
        {
            return Ok(new DistanceAndSimilarity((firstProgress + secondProgress) / 2));
        }

        var distanceAndSimilarity = 
            ImageFunctionsPerformer.GetDistanceAndSimilarityByVectors(
                firstEmbeddings, secondEmbeddings
            ).Result;

        return Ok(new DistanceAndSimilarity(1, distanceAndSimilarity[0], distanceAndSimilarity[1]));
    }

    [HttpPost("images/cancel_calculation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult CancelCalculation([FromBody] ImageIdsPair imageIdsPair)
    {
        var firstImageInfo = Db.ImageInfos.Where(
            imageInfo => imageInfo.ImageId == imageIdsPair.FirstImageId).ToList();
        var secondImageInfo = Db.ImageInfos.Where(
            imageInfo => imageInfo.ImageId == imageIdsPair.SecondImageId).ToList();  
        if (firstImageInfo.Count == 0 || secondImageInfo.Count == 0) {
            return NotFound();
        }

        var firstEmbeddings = firstImageInfo[0].Embeddings;
        var secondEmbeddings = secondImageInfo[0].Embeddings;
        if (firstEmbeddings is null && secondEmbeddings is null)
        {
            var tokens = VectorizationTaskManager.CancellationTokenSourcesByImageId;
            lock (tokens)
            {
                if (tokens.ContainsKey(firstImageInfo[0].ImageId) &&
                    tokens.ContainsKey(secondImageInfo[0].ImageId))
                {
                    tokens[firstImageInfo[0].ImageId].Cancel();
                    tokens[secondImageInfo[0].ImageId].Cancel();
                }
            }
        }

        return Ok();
    }

    [HttpGet("images")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetImageIds()
    {
        var rowImageIds = new List<string>();
        var columnImageIds = new List<string>();
        foreach (var imageInfo in Db.ImageInfos.ToList())
        {
            if (imageInfo.ImageId == EmbeddedResourcesInitializer.ADD_ICON_ID ||
                imageInfo.ImageId == EmbeddedResourcesInitializer.START_ICON_ID ||
                imageInfo.ImageId == EmbeddedResourcesInitializer.CANCEL_ICON_ID)
            {
                continue;
            }
            if (imageInfo.Type == "ROW" || imageInfo.Type == "BOTH")
            {
                rowImageIds.Add(imageInfo.ImageId);
            }
            if (imageInfo.Type == "COLUMN" || imageInfo.Type == "BOTH")
            {
                columnImageIds.Add(imageInfo.ImageId);
            }
        }
        return Ok(new ImageIdsByRowAndColumn(rowImageIds.ToArray(), columnImageIds.ToArray()));
    }

    [HttpDelete("images")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public void DeleteImages()
    {
        foreach (var item in Db.ImageInfos)
        {
            Db.ImageInfos.Remove(item);
        }
        Db.SaveChanges();
    }

    public ImageProcessorController(
        ImageInfoContext dbContext,
        IVectorizationTaskManager vectorizationTaskManager)
    {
        Db = dbContext;
        VectorizationTaskManager = (VectorizationTaskManager) vectorizationTaskManager;
        EmbeddedResourcesInitializer.AddEmbeddedImages(Db);
        MainPage = EmbeddedResourcesInitializer.GetMainPage();
    }

    private ImageInfoContext Db;
    private VectorizationTaskManager VectorizationTaskManager;
    private EmbeddedResourcesInitializer EmbeddedResourcesInitializer = new();
    private ImagesConverter ImagesConverter { get; } = new(112, 112);
    private string MainPage { get; }
}
