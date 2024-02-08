
namespace final.Models{
    [Serializable]
    public class UserNotFound : Exception
    {
        public UserNotFound(string message)
            : base(message) { }
    }

        public class DataNotValid : Exception
    {
        public DataNotValid(string message)
            : base(message) { }
    }
}