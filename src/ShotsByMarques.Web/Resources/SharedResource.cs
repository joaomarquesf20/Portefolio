namespace ShotsByMarques.Web.Resources;

/// <summary>Shared resource marker for IStringLocalizer (pt-PT default, en-US in SharedResource.en.resx).
/// MUST sit in this namespace, adjacent to SharedResource.resx in the Resources/ folder:
/// LocalizationOptions.ResourcesPath stays EMPTY, so ResourceManagerStringLocalizerFactory derives
/// the embedded-resource base name directly from the type's full name
/// (ShotsByMarques.Web.Resources.SharedResource) and resgen names the resx from this adjacent class.
/// (With ResourcesPath="Resources" the factory would double the segment:
/// "ShotsByMarques.Web.Resources.Resources.SharedResource" and every lookup would miss.)</summary>
public class SharedResource
{
}
