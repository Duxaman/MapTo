using System.Collections.Generic;
using MapTo.Integration.Tests.Data.Models;

namespace MapTo.Integration.Tests.Data.ViewModels
{
    public partial class ManagerViewModelTo : EmployeeViewModelTo
    {
        public int Level { get; set; }

        public List<EmployeeViewModelTo> Employees { get; set; } = new();
    }
}