using System;

namespace arcface_async_vectorizer.exceptions;

public class NotFound : Exception {
    public NotFound(string message) : base(message) {}
}
