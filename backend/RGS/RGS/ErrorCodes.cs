namespace RGS;

/// <summary>
/// RGS standard error codes as per specification.
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// Request processed successfully
    /// </summary>
    public const int OK = 6000;
    
    /// <summary>
    /// Bad request - invalid parameters or request format
    /// </summary>
    public const int BadRequest = 6001;
    
    /// <summary>
    /// Unauthorized - invalid session or authentication failed
    /// </summary>
    public const int Unauthorized = 6002;
    
    /// <summary>
    /// Forbidden - access denied
    /// </summary>
    public const int Forbidden = 6003;
    
    /// <summary>
    /// Not found - resource not found
    /// </summary>
    public const int NotFound = 6004;
    
    /// <summary>
    /// Method not allowed
    /// </summary>
    public const int MethodNotAllowed = 6005;
    
    /// <summary>
    /// Not acceptable
    /// </summary>
    public const int NotAcceptable = 6006;
    
    /// <summary>
    /// Request timeout
    /// </summary>
    public const int RequestTimeout = 6007;
    
    /// <summary>
    /// Conflict
    /// </summary>
    public const int Conflict = 6008;
    
    /// <summary>
    /// Gone
    /// </summary>
    public const int Gone = 6009;
    
    /// <summary>
    /// Length required
    /// </summary>
    public const int LengthRequired = 6010;
    
    /// <summary>
    /// Precondition failed
    /// </summary>
    public const int PreconditionFailed = 6011;
    
    /// <summary>
    /// Payload too large
    /// </summary>
    public const int PayloadTooLarge = 6012;
    
    /// <summary>
    /// URI too long
    /// </summary>
    public const int UriTooLong = 6013;
    
    /// <summary>
    /// Unsupported media type
    /// </summary>
    public const int UnsupportedMediaType = 6014;
    
    /// <summary>
    /// Range not satisfiable
    /// </summary>
    public const int RangeNotSatisfiable = 6015;
    
    /// <summary>
    /// Expectation failed
    /// </summary>
    public const int ExpectationFailed = 6016;
    
    /// <summary>
    /// I'm a teapot (unused, reserved)
    /// </summary>
    public const int ImATeapot = 6017;
    
    /// <summary>
    /// Misdirected request
    /// </summary>
    public const int MisdirectedRequest = 6018;
    
    /// <summary>
    /// Unprocessable entity
    /// </summary>
    public const int UnprocessableEntity = 6019;
    
    /// <summary>
    /// Locked
    /// </summary>
    public const int Locked = 6020;
    
    /// <summary>
    /// Failed dependency
    /// </summary>
    public const int FailedDependency = 6021;
    
    /// <summary>
    /// Too early
    /// </summary>
    public const int TooEarly = 6022;
    
    /// <summary>
    /// Upgrade required
    /// </summary>
    public const int UpgradeRequired = 6023;
    
    /// <summary>
    /// Precondition required
    /// </summary>
    public const int PreconditionRequired = 6024;
    
    /// <summary>
    /// Internal server error
    /// </summary>
    public const int InternalServerError = 6500;
    
    /// <summary>
    /// Balance request processed successfully
    /// </summary>
    public const int BalanceOK = 8000;
    
    /// <summary>
    /// Get error message for status code
    /// </summary>
    public static string GetMessage(int statusCode) => statusCode switch
    {
        OK => "Request processed successfully",
        BadRequest => "Bad request",
        Unauthorized => "Unauthorized",
        Forbidden => "Forbidden",
        NotFound => "Not found",
        MethodNotAllowed => "Method not allowed",
        NotAcceptable => "Not acceptable",
        RequestTimeout => "Request timeout",
        Conflict => "Conflict",
        Gone => "Gone",
        LengthRequired => "Length required",
        PreconditionFailed => "Precondition failed",
        PayloadTooLarge => "Payload too large",
        UriTooLong => "URI too long",
        UnsupportedMediaType => "Unsupported media type",
        RangeNotSatisfiable => "Range not satisfiable",
        ExpectationFailed => "Expectation failed",
        ImATeapot => "I'm a teapot",
        MisdirectedRequest => "Misdirected request",
        UnprocessableEntity => "Unprocessable entity",
        Locked => "Locked",
        FailedDependency => "Failed dependency",
        TooEarly => "Too early",
        UpgradeRequired => "Upgrade required",
        PreconditionRequired => "Precondition required",
        InternalServerError => "Internal server error",
        BalanceOK => "Request processed successfully",
        _ => "Unknown error"
    };
}
