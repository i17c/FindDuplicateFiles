﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindDuplicateFiles.Extensions
{
    public static class ExtensionMethods
    {
        public static bool IsEmpty(this string input)
        {
            return string.IsNullOrEmpty(input);
        }

        public static bool IsNotEmpty(this string input)
        {
            return !string.IsNullOrEmpty(input);
        }


    }
}