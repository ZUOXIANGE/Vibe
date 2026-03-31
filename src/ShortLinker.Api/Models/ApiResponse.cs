namespace ShortLinker.Api.Models;

public class ApiResponse<T>
{
    public int Code { get; set; } = 200;
    public string Msg { get; set; } = "success";
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, string msg = "success") => new() { Data = data, Msg = msg };
    public static ApiResponse<T> Error(string msg, int code = 400) => new() { Code = code, Msg = msg, Data = default };
}
