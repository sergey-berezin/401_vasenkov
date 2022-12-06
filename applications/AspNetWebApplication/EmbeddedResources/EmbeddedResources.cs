using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Serialization;

using Models;

namespace EmbeddedResources;

public class EmbeddedResourcesInitializer
{
    public void AddEmbeddedImages(ImageInfoContext db)
    {
        if (db.ImageInfos.Where(imageInfo => imageInfo.ImageId == ADD_ICON_ID).ToList().Count > 0)
        {
            return;
        }
        var icon = GetFileFromEmbeddedResource(_ADD_ICON_RESOURCE);
        db.ImageInfos.Add(new ImageInfo(icon, "BOTH", ADD_ICON_ID, 0));

        icon = GetFileFromEmbeddedResource(_START_ICON_RESOURCE);
        db.ImageInfos.Add(new ImageInfo(icon, "BOTH", START_ICON_ID, 1));

        icon = GetFileFromEmbeddedResource(_CANCEL_ICON_RESOURCE);
        db.ImageInfos.Add(new ImageInfo(icon, "BOTH", CANCEL_ICON_ID, 2));

        db.SaveChanges();
    }

    public string GetMainPage()
    {
        return Encoding.UTF8.GetString(GetFileFromEmbeddedResource(_MAIN_PAGE_HTML_RESOURCE));
    }

    private byte[] GetFileFromEmbeddedResource(string resourceName)
    {
        using var modelStream = typeof(EmbeddedResourcesInitializer).Assembly.GetManifestResourceStream(resourceName);
        using var memoryStream = new MemoryStream();
        modelStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public const string ADD_ICON_ID = "add-icon";
    public const string START_ICON_ID = "start-icon";
    public const string CANCEL_ICON_ID = "cancel-icon";
    private const string _MAIN_PAGE_HTML_RESOURCE = "AspNetWebApplication.EmbeddedResources.index.html";
    private const string _ADD_ICON_RESOURCE = "AspNetWebApplication.EmbeddedResources.icons.add-icon.png";
    private const string _START_ICON_RESOURCE = "AspNetWebApplication.EmbeddedResources.icons.start-icon.png";
    private const string _CANCEL_ICON_RESOURCE = "AspNetWebApplication.EmbeddedResources.icons.cancel-icon.png";
}
