﻿// OpenCppCoverage is an open source code coverage for C++.
// Copyright (C) 2016 OpenCppCoverage
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;

namespace VSPackage_UnitTests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Error.WriteLine("This function is used for " + nameof(CommandLineBuilderTests));
            Console.Error.WriteLine("To run unit tests, please use Test Explorer");

            foreach (var arg in args)
                Console.WriteLine(arg);
        }
    }
}
