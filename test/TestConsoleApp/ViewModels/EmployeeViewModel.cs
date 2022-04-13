﻿using MapTo;
using TestConsoleApp.Data.Models;
using TestConsoleApp.ViewModels2;

namespace TestConsoleApp.ViewModels
{
    [Map(typeof(Employee), MappingDirection.From)]

    public partial class EmployeeViewModel
    {
        public int Id { get; set; }

        public string EmployeeCode { get; set; }

        public ManagerViewModel Manager { get; set; }
    }
}
