namespace TwitchLogger.Website.Interfaces
{
    public interface IPasswordHasher
    {
        public string Hash(string password);
        public bool Check(string hash, string password);
    }
}
