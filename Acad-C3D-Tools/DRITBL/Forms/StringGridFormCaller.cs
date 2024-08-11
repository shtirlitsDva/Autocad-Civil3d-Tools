﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    public static class StringGridFormCaller
    {
        public static string Call(IEnumerable<string> list, string message)
        {
            var form = new Forms.StringGridForm(list, message);
            form.ShowDialog();
            return form.SelectedValue;
        }
    }
}
