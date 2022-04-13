using System;
using System.Collections.Generic;
using MapTo;
using TestConsoleApp.Data.Models;
using TestConsoleApp.ViewModels;

namespace TestConsoleApp.ViewModels2
{
    //[Map(typeof(Manager), MappingDirection.From)]
    public class ManagerViewModel : EmployeeViewModel
    {
        public int Level { get; set; }

        public IEnumerable<EmployeeViewModel> Employees { get; set; } = Array.Empty<EmployeeViewModel>();
    }
}