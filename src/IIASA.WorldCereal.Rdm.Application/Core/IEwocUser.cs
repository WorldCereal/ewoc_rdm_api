namespace IIASA.WorldCereal.Rdm.Core
{
    public interface IEwocUser
    {
        string UserId { get; }
        string Group { get; }
        bool IsAdmin { get; }
        bool IsAuthenticated { get; }
        bool CanAccessUserData { get; }
    }
}