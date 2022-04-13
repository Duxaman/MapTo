using System.Collections.Generic;
using MapTo;

namespace MapTo.Integration.Tests.Data.Models
{
    [Map(typeof(MapTo.Integration.Tests.Data.ViewModels.ManagerViewModelTo), MappingDirection.To)]
    public class ManagerTo : EmployeeTo
    {
        public int Level { get; set; }

        public List<EmployeeTo> Employees { get; set; } = new();
    }
}