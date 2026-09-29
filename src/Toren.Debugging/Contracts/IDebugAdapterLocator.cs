using Toren.Core.Results;
using Toren.Debugging.Models;

namespace Toren.Debugging.Contracts;

public interface IDebugAdapterLocator
{
    Result<DebugAdapterDescriptor> Locate();
}
