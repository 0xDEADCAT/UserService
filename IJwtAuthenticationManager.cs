    public interface IJwtAuthenticationManager
    {
        string Authenticate(string username, UserDb db);
    }
