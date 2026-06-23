namespace TwitterClone.Application.Common.Models;

/// <summary>
/// A framework-free representation of an image the caller wants to attach to a tweet. The API layer reads
/// the multipart file into this shape so the Application never depends on ASP.NET Core's <c>IFormFile</c>.
/// The bytes are held in memory (uploads are small — capped by <see cref="ImageUploadConstraints"/>).
/// </summary>
public sealed record ImageUpload(string FileName, string ContentType, long Length, byte[] Content);
