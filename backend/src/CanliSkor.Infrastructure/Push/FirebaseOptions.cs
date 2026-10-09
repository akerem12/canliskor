namespace CanliSkor.Infrastructure.Push;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// The whole service-account key file of the Firebase project, as text (Firebase console: Project settings,
    /// Service accounts, Generate new private key). A secret, kept out of the repository (on Render: the
    /// <c>Firebase__ServiceAccount</c> environment variable). Without it the Android app gets no notifications;
    /// browsers are not affected.
    /// </summary>
    public string? ServiceAccount { get; set; }
}
