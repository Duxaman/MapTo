using System.Collections.Generic;
using MapTo.Integration.Tests.Data.Models;
using MapTo;

namespace MapTo.Integration.Tests.Data.ViewModels
{
    [Map(typeof(Manager), MappingDirection.From)]
    public partial class ManagerViewModel : EmployeeViewModel
    {
        public int Level { get; set; }

        public List<EmployeeViewModel> Employees { get; set;  } = new();
    }
}