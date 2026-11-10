using System.Collections.Generic;
using System.Threading.Tasks;
using Shiftly.Shared.DTOs;

namespace Shiftly.Shared.Interfaces
{
    /// <summary>
    /// Shared service contract for Shiftly Cycle 3 business workflows:
    /// ShiftSwapRequests and SupplyOrders.
    /// Pure async contract with zero database or UI dependencies.
    /// </summary>
    public interface IShiftlyService
    {
        // Shift Swap Requests
        Task<OperationResultDto> SubmitShiftSwapRequestAsync(int authenticatedUserId, ShiftSwapRequestCreateDto request);
        Task<OperationResultDto<List<ShiftSwapItemDto>>> GetPendingShiftSwapRequestsAsync(int authenticatedUserId);
        Task<OperationResultDto> ReviewShiftSwapRequestAsync(int authenticatedUserId, ShiftSwapReviewDto review);

        // Worker Schedule & Eligible Swap Targets
        Task<OperationResultDto<List<WorkerShiftAssignmentDto>>> GetWorkerAssignmentsAsync(int authenticatedUserId);
        Task<OperationResultDto<List<CoworkerOptionDto>>> GetSwapTargetWorkersAsync(int authenticatedUserId);

        // Supply Orders
        Task<OperationResultDto<SupplyOrderItemDto>> CreateSupplyOrderAsync(int authenticatedUserId, SupplyOrderCreateDto order);
        Task<OperationResultDto<List<SupplyOrderItemDto>>> GetSupplyOrdersAsync(int authenticatedUserId);
    }
}
