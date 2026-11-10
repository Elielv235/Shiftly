using System;

namespace Shiftly.Shared.DTOs
{
    /// <summary>
    /// Read model representing a worker's assigned shift for viewing schedule and requesting swaps (Cycle 3).
    /// </summary>
    public class WorkerShiftAssignmentDto
    {
        public int AssignmentId { get; set; }
        public int ShiftId { get; set; }
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } = string.Empty;
        public bool IsConfirmed { get; set; }
        public bool HasPendingSwap { get; set; }
    }

    /// <summary>
    /// Lightweight coworker option for selecting a swap target worker (no sensitive user fields).
    /// </summary>
    public class CoworkerOptionDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    /// <summary>
    /// Transport-agnostic service result carrying HTTP-aligned status code and message.
    /// </summary>
    public class OperationResultDto
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string Message { get; set; } = string.Empty;

        public static OperationResultDto Ok(string message = "הפעולה בוצעה בהצלחה.") =>
            new() { Success = true, StatusCode = 200, Message = message };

        public static OperationResultDto Fail(int statusCode, string message) =>
            new() { Success = false, StatusCode = statusCode, Message = message };
    }

    /// <summary>
    /// Generic service result with payload.
    /// </summary>
    public class OperationResultDto<T> : OperationResultDto
    {
        public T? Data { get; set; }

        public static OperationResultDto<T> Ok(T data, string message = "הפעולה בוצעה בהצלחה.") =>
            new() { Success = true, StatusCode = 200, Message = message, Data = data };

        public new static OperationResultDto<T> Fail(int statusCode, string message) =>
            new() { Success = false, StatusCode = statusCode, Message = message, Data = default };
    }
}
