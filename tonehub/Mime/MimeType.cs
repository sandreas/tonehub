namespace tonehub.Mime;

public class MimeType
{
    public string MediaType { get; private set; } = "application";

    public string SubType { get; private set; } = "octet-stream";

    public MimeType()
    {
            
    }
    protected MimeType(string mediaType, string subType)
    {
        MediaType = mediaType;
        SubType = subType;
    }

    public bool TryParseString(string mimeTypeString)
    {
        var contentTypeParts = mimeTypeString.Split("/");
        if (contentTypeParts.Length != 2 || string.IsNullOrEmpty(contentTypeParts[0]) ||
            string.IsNullOrEmpty(contentTypeParts[1]))
        {
            return false;
        }

        MediaType = contentTypeParts[0];
        SubType = contentTypeParts[1];
        return true;
    }
        
    public static bool TryParseString(string mimeTypeString, out MimeType mimeType)
    {
        mimeType = new MimeType();
        return mimeType.TryParseString(mimeTypeString);
    }

    public override string ToString()
    {
        return $"{MediaType}/{SubType}";
    }
}