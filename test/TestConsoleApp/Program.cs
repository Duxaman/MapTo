﻿using System;
using TestConsoleApp.Data.Models;
using TestConsoleApp.ViewModels;

namespace TestConsoleApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //UserTest();
            var manager1 = new Manager
            {
                Id = 1,
                EmployeeCode = "M001",
                Level = 100
            };

            var manager2 = new Manager
            {
                Id = 2,
                EmployeeCode = "M002",
                Level = 100,
                Manager = manager1
            };

            var employee1 = new Employee
            {
                Id = 101,
                EmployeeCode = "E101",
                Manager = manager1
            };

            var employee2 = new Employee
            {
                Id = 102,
                EmployeeCode = "E102",
                Manager = manager2
            };

            manager1.Employees = new[] { employee1, manager2 };
            manager2.Employees = new[] { employee2 };

            var manager1ViewModel = manager1.ToManagerViewModel();
            int a = 0;
        }

        private static void UserTest()
        {
            var user = new User
            {
                Id = 1234,
                RegisteredAt = DateTimeOffset.Now,
                Profile = new Profile
                {
                    FirstName = "John",
                    LastName = "Doe"
                }
            };

            var vm = user.ToUserViewModel();

            Console.WriteLine("Key: {0}", vm.Key);
            Console.WriteLine("RegisteredAt: {0}", vm.RegisteredAt);
            Console.WriteLine("FirstName: {0}", vm.Profile.FirstName);
            Console.WriteLine("LastName: {0}", vm.Profile.LastName);
        }
    }
}