using System;

namespace ArcfaceImageVectorizer.Exceptions;

public class NotFound : Exception {
    public NotFound(string message) : base(message) {}
}
