﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.LanguageServices

Namespace Microsoft.CodeAnalysis.VisualBasic
#If MEF Then
    <ExportLanguageService(GetType(ICommandLineArgumentsFactoryService), LanguageNames.VisualBasic)>
    Class VisualBasicCommandLineArgumentsFactoryService
#Else
    Class VisualBasicCommandLineArgumentsFactoryService
#End If
        Implements ICommandLineArgumentsFactoryService

        Public Function CreateCommandLineArguments(arguments As IEnumerable(Of String), baseDirectory As String, isInteractive As Boolean) As CommandLineArguments Implements ICommandLineArgumentsFactoryService.CreateCommandLineArguments
            Dim parser = If(isInteractive, VisualBasicCommandLineParser.Interactive, VisualBasicCommandLineParser.Default)
            Return parser.Parse(arguments, baseDirectory)
        End Function
    End Class
End Namespace