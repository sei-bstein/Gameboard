using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class PropertyBuilderExtensions
{
    // Probably never decrease this - it could result in loss of data.
    // Increasing it is okay, but if you do, it'll affect most ID fields in the DB.
    private const int STANDARD_ID_MAXLENGTH = 40;

    public static PropertyBuilder HasStandardIdMaxLength(this PropertyBuilder builder)
    {
        return builder.HasMaxLength(STANDARD_ID_MAXLENGTH);
    }
}