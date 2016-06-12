Imports System.Security
Imports System.Security.Cryptography
Imports System.Security.Cryptography.Pkcs
Imports System.Security.Cryptography.X509Certificates

Public Class CertificadosX509Lib
    Public Shared Function FirmaBytesMensaje(ByVal argBytesMsg As Byte(), ByVal argCertFirmante As X509Certificate2) As Byte()
        Try
            ' Pongo el mensaje en un objeto ContentInfo (requerido para construir el obj SignedCms)
            Dim infoContenido As New ContentInfo(argBytesMsg)
            Dim cmsFirmado As New SignedCms(infoContenido)
            ' Creo objeto CmsSigner que tiene las caracteristicas del firmante
            Dim cmsFirmante As New CmsSigner(argCertFirmante)
            cmsFirmante.IncludeOption = X509IncludeOption.EndCertOnly
            ' Firmo el mensaje PKCS #7
            cmsFirmado.ComputeSignature(cmsFirmante)
            ' Encodeo el mensaje PKCS #7.
            Return cmsFirmado.Encode()
        Catch excepcionAlFirmar As Exception
            Throw New Exception("***Error al firmar: " & excepcionAlFirmar.Message)
            Return Nothing
        End Try
    End Function

    Public Shared Function ObtieneCertificadoDesdeArchivo(ByVal argArchivo As String, ByVal argPassword As SecureString) As X509Certificate2
        Dim objCert As New X509Certificate2
        Try
            If argPassword.IsReadOnly Then
                objCert.Import(My.Computer.FileSystem.ReadAllBytes(argArchivo), argPassword, X509KeyStorageFlags.PersistKeySet)
            Else
                objCert.Import(My.Computer.FileSystem.ReadAllBytes(argArchivo))
            End If
            Return objCert
        Catch excepcionAlImportarCertificado As Exception
            Throw New Exception(excepcionAlImportarCertificado.Message & " " & excepcionAlImportarCertificado.StackTrace)
            Return Nothing
        End Try
    End Function
End Class

