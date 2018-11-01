using Binarysharp.Assemblers.Fasm;
using System;
using System.CodeDom;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Process.NET;
using Process.NET.Memory;
using Process.NET.Native.Types;
using Process = System.Diagnostics.Process;

namespace CSharp_Inline_Assembly
{
    /// <summary>
    /// This program demonstrates how to use inline x86 assembly from C#
    /// </summary>
    class Program
    {
        private static IProcess _currentProcess;

        static void Main(string[] args)
        {
            _currentProcess = new ProcessSharp(System.Diagnostics.Process.GetCurrentProcess(), MemoryType.Local);

            Example1();
            Example2();
            Example3();
            Example4();
            Example5();

            //Wait for any key to exit
            Console.ReadKey(true);
        }

        // Example 1: Function Returning Constant Value
        [SuppressUnmanagedCodeSecurity] // disable security checks for better performance
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] // cdecl - let caller (.NET CLR) clean the stack
        private delegate int AssemblyConstantValueFunction();
        private static void Example1()
        {
            const int valueToReturn = 1;

            FasmNet fasmNet = new FasmNet();
            fasmNet.AddLine("use32"); //Tell FASM.Net to use x86 (32bit) mode
            fasmNet.AddLine("mov eax, {0}", valueToReturn); // copy "1" to eax
            fasmNet.AddLine("ret"); // in cdecl calling convention, return value is stored in eax; so this will return 1

            byte[] assembledCode = fasmNet.Assemble();

            var allocatedCodeMemory = _currentProcess.MemoryFactory.Allocate(
                name: "Example1", // only used for debugging; not really needed
                size: assembledCode.Length, 
                protection: MemoryProtectionFlags.ExecuteReadWrite /* It is important to mark the memory as executeable or we will get exceptions from DEP */
            );
            allocatedCodeMemory.Write(0, assembledCode);

            var myAssemblyFunction = Marshal.GetDelegateForFunctionPointer<AssemblyConstantValueFunction>(allocatedCodeMemory.BaseAddress);
            var returnValue = myAssemblyFunction();

            // Warning: Potential memory leak!
            // Do not forget to dispose the allocated code memory after usage. 
            allocatedCodeMemory.Dispose();

            Console.WriteLine($"Example1 return value: {returnValue}, expected: {valueToReturn}"); // Prints 1
        }

        // Example 2: Function Reading Registers
        [SuppressUnmanagedCodeSecurity] // disable security checks for better performance
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] // cdecl - let caller (.NET CLR) clean the stack
        private delegate IntPtr AssemblyReadRegistersFunction();
        private static void Example2()
        {
            FasmNet fasmNet = new FasmNet();
            fasmNet.AddLine("use32"); //Tell FASM.Net to use x86 (32bit) mode
            fasmNet.AddLine("mov eax, [ebp+4]"); // Set return value to ebp+4 (return address)
            fasmNet.AddLine("ret"); // in cdecl calling convention, return value is stored in eax; so this will return the return address

            byte[] assembledCode = fasmNet.Assemble();

            var allocatedCodeMemory = _currentProcess.MemoryFactory.Allocate(
                name: "Example2", // only used for debugging; not really needed
                size: assembledCode.Length,
                protection: MemoryProtectionFlags.ExecuteReadWrite /* It is important to mark the memory as executeable or we will get exceptions from DEP */
            );
            allocatedCodeMemory.Write(0, assembledCode);

            var myAssemblyFunction = Marshal.GetDelegateForFunctionPointer<AssemblyReadRegistersFunction>(allocatedCodeMemory.BaseAddress);
            var returnValue = myAssemblyFunction();

            // Warning: Potential memory leak!
            // Do not forget to dispose the allocated code memory after usage. 
            allocatedCodeMemory.Dispose();

            Console.WriteLine($"Example2 return value: 0x{returnValue.ToInt32():X}"); // Prints this methods JIT'ed address
        }

        // Example 3: Add Function With Parameters
        [SuppressUnmanagedCodeSecurity] // disable security checks for better performance
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] // cdecl - let caller (.NET CLR) clean the stack
        private delegate int AssemblyAddFunction(int x, int y);
        private static void Example3()
        {
            FasmNet fasmNet = new FasmNet();
            fasmNet.AddLine("use32"); //Tell FASM.Net to use x86 (32bit) mode
            fasmNet.AddLine("push ebp"); // init stack frame
            fasmNet.AddLine("mov eax, [ebp+8]"); // set eax to second param (remember, in cdecl calling convention, params are pushed right-to-left)
            fasmNet.AddLine("mov edx, [ebp+12]"); // set edx to first param
            fasmNet.AddLine("add eax, edx"); //add edx (first param) to eax (second param) 
            fasmNet.AddLine("pop ebp"); // leave stack frame
            fasmNet.AddLine("ret");  // in cdecl calling convention, return value is stored in eax; so this will return both params added up

            byte[] assembledCode = fasmNet.Assemble();

            var allocatedCodeMemory = _currentProcess.MemoryFactory.Allocate(
                name: "Example3", // only used for debugging; not really needed
                size: assembledCode.Length,
                protection: MemoryProtectionFlags.ExecuteReadWrite /* It is important to mark the memory as executeable or we will get exceptions from DEP */
            );
            allocatedCodeMemory.Write(0, assembledCode);

            var myAssemblyFunction = Marshal.GetDelegateForFunctionPointer<AssemblyAddFunction>(allocatedCodeMemory.BaseAddress);
            var returnValue = myAssemblyFunction(10, -15);

            // Warning: Potential memory leak!
            // Do not forget to dispose the allocated code memory after usage. 
            allocatedCodeMemory.Dispose();

            Console.WriteLine($"Example3 return value: {returnValue}, expected: -5"); // Prints -5
        }

        // Example 4: Add Function With Parameters (Without Fasm.NET)
        private static void Example4()
        {
            //You can use any x86 assembler
            //For this example I have used https://defuse.ca/online-x86-assembler.htm

            // Without FASM.Net I strongly suggest you to comment each instruction (e.g. "0 push ebp")
            byte[] assembledCode =
            {
                0x55,               // 0 push ebp            ; init stack frame
                0x8B, 0x45, 0x08,   // 1 mov  eax, [ebp+8]   ; set eax to second param (remember, in cdecl calling convention, params are pushed right-to-left)
                0x8B, 0x55, 0x0C,   // 4 mov  edx, [ebp+12]  ; set edx to first param
                0x01, 0xD0,         // 7 add  eax, edx       ; add edx (first param) to eax (second param) 
                0x5D,               // 9 pop  ebp            ; leave stack frame
                0xC3                // A ret                 ; in cdecl calling convention, return value is stored in eax; so this will return both params added up
            };

            var allocatedCodeMemory = _currentProcess.MemoryFactory.Allocate(
                name: "Example4", // only used for debugging; not really needed
                size: assembledCode.Length,
                protection: MemoryProtectionFlags.ExecuteReadWrite /* It is important to mark the memory as executeable or we will get exceptions from DEP */
            );
            allocatedCodeMemory.Write(0, assembledCode);

            var myAssemblyFunction = Marshal.GetDelegateForFunctionPointer<AssemblyAddFunction>(allocatedCodeMemory.BaseAddress);
            var returnValue = myAssemblyFunction(10, -15);

            // Warning: Potential memory leak!
            // Do not forget to dispose the allocated code memory after usage. 
            allocatedCodeMemory.Dispose();

            Console.WriteLine($"Example3 (no Fasm.NET) return value: {returnValue}, expected: -5"); // Prints -5
        }

        //Example 5: Add Function With Parameters (Without any dependencies)
        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        private static void Example5()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            //You can use any x86 assembler
            //For this example I have used https://defuse.ca/online-x86-assembler.htm

            // Without FASM.Net I strongly suggest you to comment each instruction (e.g. "0 push ebp")
            byte[] assembledCode =
            {
                0x55,               // 0 push ebp            ; init stack frame
                0x8B, 0x45, 0x08,   // 1 mov  eax, [ebp+8]   ; set eax to second param (remember, in cdecl calling convention, params are pushed right-to-left)
                0x8B, 0x55, 0x0C,   // 4 mov  edx, [ebp+12]  ; set edx to first param
                0x01, 0xD0,         // 7 add  eax, edx       ; add edx (first param) to eax (second param) 
                0x5D,               // 9 pop  ebp            ; leave stack frame
                0xC3                // A ret                 ; in cdecl calling convention, return value is stored in eax; so this will return both params added up
            };

            int returnValue;
            unsafe
            {
                fixed (byte* ptr = assembledCode)
                {
                    var memoryAddress = (IntPtr) ptr;

                    // Mark memory as EXECUTE_READWRITE to prevent DEP exceptions
                    if (!VirtualProtectEx(process.Handle, memoryAddress,
                        (UIntPtr) assembledCode.Length, 0x40 /* EXECUTE_READWRITE */, out uint _))
                    {
                        throw new Win32Exception();
                    }

                    var myAssemblyFunction = Marshal.GetDelegateForFunctionPointer<AssemblyAddFunction>(memoryAddress);
                    returnValue = myAssemblyFunction(10, -15);
                }               
            }

            // Note: We do not have to dispose memory ourself; the CLR will handle this.  
            Console.WriteLine($"Example3 (no dependencies) return value: {returnValue}, expected: -5"); // Prints -5
        }
    }
}

 