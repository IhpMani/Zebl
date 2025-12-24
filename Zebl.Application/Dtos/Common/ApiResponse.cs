namespace Zebl.Application.Dtos.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public T? Data { get; set; }
        public object? Meta { get; set; }
    }
}
