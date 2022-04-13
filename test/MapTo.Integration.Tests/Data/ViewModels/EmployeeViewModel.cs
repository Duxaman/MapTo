﻿using MapTo.Integration.Tests.Data.Models;
using MapTo;

namespace MapTo.Integration.Tests.Data.ViewModels
{
    [Map(typeof(Employee), MappingDirection.From)]
    public partial class EmployeeViewModel
    {
        public int Id { get; set; }

        public string EmployeeCode { get; set; }

        public ManagerViewModel Manager { get; set; }
    }
}