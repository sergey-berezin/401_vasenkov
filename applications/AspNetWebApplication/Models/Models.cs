using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Models;

[Index(nameof(ImageInfo.ImageHash), IsUnique = false)]
public class ImageInfo
{
    [Key]
    public string ImageId { get; set; }
    public long ImageHash { get; set; }
    public byte[] Image { get; set; }
    public string Type { get; set; }    // ROW / COLUMN / BOTH
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? InternalData { get; set; } = null;
    [NotMapped]
    public float[]? Embeddings
    {
        get
        {
            if (InternalData is null)
            {
                return null;
            }
            var values = this.InternalData.Split(',');
            var embeddings = new List<float>();
            foreach (var value in values)
            {
                embeddings.Add(float.Parse(value));
            }
            return embeddings.ToArray();
        }
        set
        {
            var stringValues = new List<string>();
            foreach (var curValue in value)
            {
                stringValues.Add(curValue.ToString());
            }
            this.InternalData = string.Join(",", stringValues);
        }
    }

    public ImageInfo(byte[] image, string type, string imageId, long imageHash)
    {
        Image = image;
        Type = type;
        ImageId = imageId;
        ImageHash = imageHash;
    }
}

public class ImageInfoContext : DbContext
{
    public DbSet<ImageInfo> ImageInfos { get; set; }
    public ImageInfoContext(DbContextOptions<ImageInfoContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }
}
