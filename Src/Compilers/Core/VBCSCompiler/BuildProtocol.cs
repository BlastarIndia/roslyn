﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// This file describes data structures about the protocol from client program to server that is 
// used. The basic protocol is this.
//
// Server creates a named pipe with name ProtocolConstants.PipeName, with the server process id
// appended to that pipe name.
//
// Client enumerates all processes on the machine, search for one with the correct fully qualified
// executable name. If none are found, that executable is started. The client then connects
// to the named pipe, and writes a single request, as represented by the Request structure.
// If a pipe is disconnected, and it didn't create that process, the clients continues trying to 
// connect.
//
// After the server pipe is connected, it forks off a thread to handle the connection, and creates
// a new instance of the pipe to listen for new clients. When it gets a request, it validates
// the security and elevation level of the client. If that fails, it disconnects the client. Otherwise,
// it handles the request, sends a response (described by Response class) back to the client, then
// disconnects the pipe and ends the thread.
//
// NOTE: Changes to the protocol information in this file must also be reflected in protocol.h in the
// unmanaged rcsc project, as well as the code in protocol.cpp.

namespace Microsoft.CodeAnalysis.CompilerServer
{
    /// <summary>
    /// Represents a request from the client. A request is as follows.
    /// 
    ///  Field Name         Type            Size (bytes)
    /// --------------------------------------------------
    ///  Length             Integer         4
    ///  ID                 UInteger        4
    ///  Argument Count     UInteger        4
    ///  Arguments          Argument[]      Variable
    /// 
    /// See <see cref="Argument"/> for the format of an
    /// Argument.
    /// 
    /// </summary>
    internal class BuildRequest
    {
        public readonly uint Id;
        public readonly ImmutableArray<Argument> Arguments;

        public BuildRequest(uint requestId, ImmutableArray<Argument> arguments)
        {
            this.Id = requestId;

            if (arguments.Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException("arguments",
                    "Too many arguments: maximum of "
                    + ushort.MaxValue + " arguments allowed.");
            }
            this.Arguments = arguments;
        }

        /// <summary>
        /// Read a Request from the given stream.
        /// 
        /// The total request size must be less than 1MB.
        /// </summary>
        /// <returns>null if the Request was too large, the Request otherwise.</returns>
        public static async Task<BuildRequest> ReadAsync(PipeStream inStream, CancellationToken cancellationToken)
        {
            // Read the length of the request
            var lengthBuffer = new byte[4];
            CompilerServerLogger.Log("Reading length of request");
            await BuildProtocolConstants.ReadAllAsync(inStream, lengthBuffer, 4, cancellationToken).ConfigureAwait(false);
            var length = BitConverter.ToInt32(lengthBuffer, 0);

            // Back out if the request is > 1MB
            if (length > 0x100000)
            {
                CompilerServerLogger.Log("Request is over 1MB in length, cancelling read.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Read the full request
            var responseBuffer = new byte[length];
            await BuildProtocolConstants.ReadAllAsync(inStream, responseBuffer, length, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            CompilerServerLogger.Log("Parsing request");
            // Parse the request into the Request data structure.
            using (var reader = new BinaryReader(new MemoryStream(responseBuffer), Encoding.Unicode))
            {
                uint requestId = reader.ReadUInt32();
                uint argumentCount = reader.ReadUInt32();

                var argumentsBuilder = ImmutableArray.CreateBuilder<Argument>((int)argumentCount);

                for (int i = 0; i < argumentCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    argumentsBuilder.Add(BuildRequest.Argument.ReadFromBinaryReader(reader));
                }

                return new BuildRequest(requestId, argumentsBuilder.ToImmutable());
            }
        }

        /// <summary>
        /// Write a Request to the stream.
        /// </summary>
        public async Task WriteAsync(PipeStream outStream, CancellationToken cancellationToken)
        {
            using (var writer = new BinaryWriter(new MemoryStream(), Encoding.Unicode))
            {
                // Format the request.
                CompilerServerLogger.Log("Formatting request");
                writer.Write(this.Id);
                writer.Write(this.Arguments.Length);
                foreach (Argument arg in this.Arguments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    arg.WriteToBinaryWriter(writer);
                }
                writer.Flush();

                cancellationToken.ThrowIfCancellationRequested();


                // Grab the MemoryStream and its internal buffer
                // to prevent making another copy.
                var stream = (MemoryStream)writer.BaseStream;
                // Write the length of the request
                int length = (int)stream.Length;

                // Back out if the request is > 1 MB
                if (stream.Length > 0x100000)
                {
                    CompilerServerLogger.Log("Request is over 1MB in length, cancelling write");
                    throw new ArgumentOutOfRangeException();
                }

                // Send the request to the server
                CompilerServerLogger.Log("Writing length of request.");
                await outStream.WriteAsync(BitConverter.GetBytes(length), 0, 4,
                                           cancellationToken).ConfigureAwait(false);

                CompilerServerLogger.Log("Writing request of size {0}", length);
                // Write the request
                await outStream.WriteAsync(stream.GetBuffer(), 0, length,
                                           cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// A command line argument to the compilation. 
        /// An argument is formatted as follows:
    	/// 
    	///  Field Name         Type            Size (bytes)
    	/// --------------------------------------------------
    	///  ID                 UInteger        4
    	///  Index              UInteger        4
    	///  Value              String          Variable
    	/// 
    	/// Strings are encoded via a length prefix as a signed
    	/// 32-bit integer, followed by an array of characters.
        /// </summary>
        public struct Argument
        {
            public readonly uint ArgumentId;
            public readonly uint ArgumentIndex;
            public readonly string Value;

            public Argument(uint argumentId, uint argumentIndex, string value)
            {
                this.ArgumentId = argumentId;
                this.ArgumentIndex = argumentIndex;
                this.Value = value;
            }

            public static Argument ReadFromBinaryReader(BinaryReader reader)
            {
                uint argId = reader.ReadUInt32();
                uint argIndex = reader.ReadUInt32();
                string value = BuildProtocolConstants.ReadLengthPrefixedString(reader);
                return new Argument(argId, argIndex, value);
            }

            public void WriteToBinaryWriter(BinaryWriter writer)
            {
                writer.Write(this.ArgumentId);
                writer.Write(this.ArgumentIndex);
                BuildProtocolConstants.WriteLengthPrefixedString(writer, this.Value);
            }
        }
    }

    /// <summary>
    /// Represents a Response from the server. A response is as follows.
    /// 
    ///  Field Name         Type            Size (bytes)
    /// --------------------------------------------------
    ///  Length             UInteger        4
    ///  ReturnCode         Integer         4
    ///  Output             String          Variable
    ///  ErrorOutput        String          Variable
    /// 
    /// Strings are encoded via a length prefix as a signed
    /// 32-bit integer, followed by an array of characters.
    /// 
    /// </summary>
    internal class BuildResponse
    {
        public readonly int ReturnCode;
        public readonly string Output;
        public readonly string ErrorOutput;

        public BuildResponse(int returnCode, string output, string errorOutput)
        {
            this.ReturnCode = returnCode;
            this.Output = output;
            this.ErrorOutput = errorOutput;
        }

        /// <summary>
        /// May throw exceptions if there are pipe problems.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<BuildResponse> ReadAsync(PipeStream stream, CancellationToken cancellationToken)
        {
            CompilerServerLogger.Log("Reading response length");
            // Read the response length
            var lengthBuffer = new byte[4];
            await BuildProtocolConstants.ReadAllAsync(stream, lengthBuffer, 4, cancellationToken).ConfigureAwait(false);
            var length = BitConverter.ToUInt32(lengthBuffer, 0);

            // Read the response
            CompilerServerLogger.Log("Reading response of length {0}", length);
            var responseBuffer = new byte[length];
            await BuildProtocolConstants.ReadAllAsync(stream,
                                                      responseBuffer,
                                                      responseBuffer.Length,
                                                      cancellationToken).ConfigureAwait(false);

            using (var reader = new BinaryReader(new MemoryStream(responseBuffer), Encoding.Unicode))
            {
                int returnCode = reader.ReadInt32();
                string output = BuildProtocolConstants.ReadLengthPrefixedString(reader);
                string errorOutput = BuildProtocolConstants.ReadLengthPrefixedString(reader);

                return new BuildResponse(returnCode, output, errorOutput);
            }
        }

        public async Task WriteAsync(PipeStream outStream, CancellationToken cancellationToken)
        {
            using (var writer = new BinaryWriter(new MemoryStream(), Encoding.Unicode))
            {
                // Format the response
                CompilerServerLogger.Log("Formatting Response");
                writer.Write(this.ReturnCode);
                BuildProtocolConstants.WriteLengthPrefixedString(writer, this.Output);
                BuildProtocolConstants.WriteLengthPrefixedString(writer, this.ErrorOutput);
                writer.Flush();

                cancellationToken.ThrowIfCancellationRequested();

                // Send the response to the client

                // Grab the MemoryStream and its internal buffer to prevent
                // making another copy.
                var stream = (MemoryStream)writer.BaseStream;
                // Write the length of the response
                uint length = (uint)stream.Length;
                CompilerServerLogger.Log("Writing response length");
                // There is no way to know the number of bytes written to
                // the pipe stream. We just have to assume all of them are written.
                await outStream.WriteAsync(BitConverter.GetBytes(length),
                                           0,
                                           4,
                                           cancellationToken).ConfigureAwait(false);

                // Write the response
                CompilerServerLogger.Log("Writing response of size {0}", length);
                // There is no way to know the number of bytes written to
                // the pipe stream. We just have to assume all of them are written.
                await outStream.WriteAsync(stream.GetBuffer(),
                                           0,
                                           (int)length,
                                           cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Constants about the protocol.
    /// </summary>
    internal static class BuildProtocolConstants
    {
        /// <summary>
        /// The name of the executable.
        /// </summary>
        public const string ServerExeName = "VBCSCompiler.exe";

        /// <summary>
        /// The name of the named pipe. A process id is appended to the end.
        /// </summary>
        public const string PipeName = "VBCSCompiler";

        // The id numbers below are just random. It's useful to use id numbers
        // that won't occur accidentally for debugging.

        // csc -- compile C#
        public const int RequestId_CSharpCompile = 0x44532521;

        // vbc -- compiler VB
        public const int RequestId_VisualBasicCompile = 0x44532522;

        // analyzer -- analyze managed code
        public const int RequestId_Analyze = 0x44532523;

        // Arugments for CSharp and VB Compiler

        // The current directory of the client
        public const int ArgumentId_CurrentDirectory = 0x51147221;

        // A comment line argument. The argument index indicates which one (0 .. N)
        public const int ArgumentId_CommandLineArgument = 0x51147222;

        // The "LIB" environment variable of the client
        public const int ArgumentId_LibEnvVariable = 0x51147223;

        /// <summary>
        /// Read a string from the Reader where the string is encoded
        /// as a length prefix (signed 32-bit integer) followed by
        /// a sequence of characters.
        /// </summary>
        public static string ReadLengthPrefixedString(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            return new String(reader.ReadChars(length));
        }

        /// <summary>
        /// Write a string to the Writer where the string is encoded
        /// as a length prefix (signed 32-bit integer) follows by
        /// a sequence of characters.
        /// </summary>
        public static void WriteLengthPrefixedString(BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            writer.Write(value.ToCharArray());
        }

        /// <summary>
        /// This task does not complete until we are completely done reading.
        /// </summary>
        internal static async Task ReadAllAsync(
            PipeStream stream,
            byte[] buffer,
            int count,
            CancellationToken cancellationToken)
        {
            int totalBytesRead = 0;
            do
            {
                CompilerServerLogger.Log("Attempting to read {0} bytes from the stream",
                    count - totalBytesRead);
                int bytesRead = await stream.ReadAsync(buffer,
                                                       totalBytesRead,
                                                       count - totalBytesRead,
                                                       cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    CompilerServerLogger.Log("Unexpected -- read 0 bytes from the stream.");
                    throw new EndOfStreamException("Reached end of stream before end of read.");
                }
                CompilerServerLogger.Log("Read {0} bytes", bytesRead);
                totalBytesRead += bytesRead;
            } while (totalBytesRead < count);
            CompilerServerLogger.Log("Finished read");
        }
    }
}
