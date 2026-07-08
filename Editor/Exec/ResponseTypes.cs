namespace UnityExec
{
    public class SuccessResponse
    {
        public bool success = true;
        public string message;
        public object data;

        public SuccessResponse(string message, object data = null)
        {
            this.message = message;
            this.data = data;
        }
    }

    public class ErrorResponse
    {
        public bool success = false;
        public string error;

        public ErrorResponse(string error)
        {
            this.error = error;
        }
    }
}
