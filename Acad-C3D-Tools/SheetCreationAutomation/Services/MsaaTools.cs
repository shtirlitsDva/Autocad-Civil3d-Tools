using Accessibility;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SheetCreationAutomation.Services
{
    internal static class MsaaTools
    {
        private const uint ObjidClient = 0xFFFFFFFC;
        private const int ChildIdSelf = 0;

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(
            IntPtr hwnd,
            uint dwObjectId,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

        [DllImport("oleacc.dll")]
        private static extern int AccessibleChildren(
            IAccessible paccContainer,
            int iChildStart,
            int cChildren,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] rgvarChildren,
            out int pcObtained);

        public static bool TrySetValueByChildPath(
            IntPtr hwnd,
            IReadOnlyList<int> oneBasedPath,
            string value,
            out string error)
        {
            error = string.Empty;

            if (hwnd == IntPtr.Zero)
            {
                error = "Window handle is zero.";
                return false;
            }

            if (oneBasedPath == null || oneBasedPath.Count == 0)
            {
                error = "MSAA child path is empty.";
                return false;
            }

            try
            {
                if (!TryGetRootAccessible(hwnd, out IAccessible? root, out error) || root == null)
                {
                    return false;
                }

                var current = new MsaaNode(root, ChildIdSelf);
                for (int i = 0; i < oneBasedPath.Count; i++)
                {
                    int childIndex = oneBasedPath[i];
                    if (childIndex <= 0)
                    {
                        error = $"Invalid child index at depth {i}: {childIndex}.";
                        return false;
                    }

                    if (!TryGetChildAt(current, childIndex, out MsaaNode? next, out error) || next == null)
                    {
                        error = $"Path resolution failed at depth {i}, index={childIndex}. {error}";
                        return false;
                    }

                    current = next;
                }

                current.Accessible.set_accValue(current.ChildId, value);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryGetRootAccessible(IntPtr hwnd, out IAccessible? root, out string error)
        {
            error = string.Empty;
            root = null;

            Guid iidIAccessible = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
            int hr = AccessibleObjectFromWindow(hwnd, ObjidClient, ref iidIAccessible, out object accessibleObject);
            if (hr < 0)
            {
                error = $"AccessibleObjectFromWindow failed. HRESULT=0x{hr:X8}";
                return false;
            }

            root = accessibleObject as IAccessible;
            if (root == null)
            {
                error = "Root MSAA object is not IAccessible.";
                return false;
            }

            return true;
        }

        private static bool TryGetChildAt(
            MsaaNode parent,
            int oneBasedIndex,
            out MsaaNode? child,
            out string error)
        {
            error = string.Empty;
            child = null;

            IAccessible container = parent.Accessible;
            if (parent.ChildId is int childId && childId != ChildIdSelf)
            {
                object branch = container.get_accChild(childId);
                if (branch is IAccessible branchAcc)
                {
                    container = branchAcc;
                }
                else
                {
                    error = $"MSAA node childId={childId} is not traversable.";
                    return false;
                }
            }

            object[] result = new object[1];
            int obtained = 0;
            int hr = AccessibleChildren(container, oneBasedIndex - 1, 1, result, out obtained);
            if (hr < 0 || obtained == 0 || result[0] == null)
            {
                error = $"AccessibleChildren failed (hr=0x{hr:X8}, obtained={obtained}).";
                return false;
            }

            object rawChild = result[0];
            if (rawChild is IAccessible childAcc)
            {
                child = new MsaaNode(childAcc, ChildIdSelf);
                return true;
            }

            if (rawChild is int rawChildId)
            {
                object indirect = container.get_accChild(rawChildId);
                if (indirect is IAccessible indirectAcc)
                {
                    child = new MsaaNode(indirectAcc, ChildIdSelf);
                    return true;
                }

                child = new MsaaNode(container, rawChildId);
                return true;
            }

            error = $"Unsupported MSAA child object type: {rawChild.GetType().FullName}.";
            return false;
        }

        private sealed class MsaaNode
        {
            public MsaaNode(IAccessible accessible, object childId)
            {
                Accessible = accessible;
                ChildId = childId;
            }

            public IAccessible Accessible { get; }
            public object ChildId { get; }
        }
    }
}
