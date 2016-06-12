Imports System.Xml
Imports System.Security
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Net

Public Class LoginTicket
    Public CUIT As Long 'El Cuit.
    Public UniqueId As UInt32 ' Entero de 32 bits sin signo que identifica el requerimiento
    Public GenerationTime As DateTime ' Momento en que fue generado el requerimiento
    Public ExpirationTime As DateTime ' Momento en el que exoira la solicitud
    Public Service As String ' Identificacion del WSN para el cual se solicita el TA
    Public Sign As String ' Firma de seguridad recibida en la respuesta
    Public Token As String ' Token de seguridad recibido en la respuesta
    Public XmlLoginTicketRequest As XmlDocument = Nothing
    Public XmlLoginTicketResponse As XmlDocument = Nothing
    Public RutaDelCertificadoFirmante As String
    Public XmlStrLoginTicketRequestTemplate As String = "<loginTicketRequest><header><uniqueId></uniqueId><generationTime></generationTime><expirationTime></expirationTime></header><service></service></loginTicketRequest>"
    Private Shared _globalUniqueID As UInt32 = 0 ' OJO! NO ES THREAD-SAFE

    Public Sub New(ByVal ArchivoXML As XmlDocument, ByVal ruta As String)
        Try
            UniqueId = UInt32.Parse(ArchivoXML.SelectSingleNode("//uniqueId").InnerText)
            GenerationTime = DateTime.Parse(ArchivoXML.SelectSingleNode("//generationTime").InnerText)
            ExpirationTime = DateTime.Parse(ArchivoXML.SelectSingleNode("//expirationTime").InnerText)
            Sign = ArchivoXML.SelectSingleNode("//sign").InnerText
            Token = ArchivoXML.SelectSingleNode("//token").InnerText
            RutaDelCertificadoFirmante = ruta
        Catch ex As Exception
            Throw New Exception("No se puede inicializar el constructor sobrecargado de LoginTicket")
        End Try
    End Sub

    Public Sub New()
    End Sub

    Public Function ObtenerLoginTicketResponse(ByVal argServicio As String, ByVal argUrlWsaa As String, ByVal argRutaCertX509Firmante As String, _
                                                ByVal argPassword As SecureString, ByVal argProxy As String, ByVal argProxyUser As String, _
                                                ByVal argProxyPassword As String, ByVal argVerbose As Boolean) As String

        Me.RutaDelCertificadoFirmante = argRutaCertX509Firmante
        Dim cmsFirmadoBase64 As String
        Dim loginTicketResponse As String
        Dim xmlNodoUniqueId As XmlNode
        Dim xmlNodoGenerationTime As XmlNode
        Dim xmlNodoExpirationTime As XmlNode
        Dim xmlNodoService As XmlNode

        ' PASO 1: Genero el Login Ticket Request
        Try
            _globalUniqueID += 1
            XmlLoginTicketRequest = New XmlDocument()
            XmlLoginTicketRequest.LoadXml(XmlStrLoginTicketRequestTemplate)
            xmlNodoUniqueId = XmlLoginTicketRequest.SelectSingleNode("//uniqueId")
            xmlNodoGenerationTime = XmlLoginTicketRequest.SelectSingleNode("//generationTime")
            xmlNodoExpirationTime = XmlLoginTicketRequest.SelectSingleNode("//expirationTime")
            xmlNodoService = XmlLoginTicketRequest.SelectSingleNode("//service")
            xmlNodoGenerationTime.InnerText = DateTime.Now.AddMinutes(-10).ToString("s")
            xmlNodoExpirationTime.InnerText = DateTime.Now.AddMinutes(+10).ToString("s")
            xmlNodoUniqueId.InnerText = CStr(_globalUniqueID)
            xmlNodoService.InnerText = argServicio
            Me.Service = argServicio
        Catch excepcionAlGenerarLoginTicketRequest As Exception
            Throw New Exception("***Error GENERANDO el LoginTicketRequest : " + excepcionAlGenerarLoginTicketRequest.Message + excepcionAlGenerarLoginTicketRequest.StackTrace)
        End Try

        ' PASO 2: Firmo el Login Ticket Request
        Try
            Dim certFirmante As X509Certificate2 = CertificadosX509Lib.ObtieneCertificadoDesdeArchivo(RutaDelCertificadoFirmante, argPassword)
            ' Convierto el login ticket request a bytes, para firmar
            Dim EncodedMsg As Encoding = Encoding.UTF8
            Dim msgBytes As Byte() = EncodedMsg.GetBytes(XmlLoginTicketRequest.OuterXml)
            ' Firmo el msg y paso a Base64
            Dim encodedSignedCms As Byte() = CertificadosX509Lib.FirmaBytesMensaje(msgBytes, certFirmante)
            cmsFirmadoBase64 = Convert.ToBase64String(encodedSignedCms)
        Catch excepcionAlFirmar As Exception
            Throw New Exception("***Error FIRMANDO el LoginTicketRequest : " + excepcionAlFirmar.Message)
        End Try
        ' PASO 3: Invoco al WSAA para obtener el Login Ticket Response
        Try
            Dim servicioWsaa As New Wsaa.LoginCMSService()
            servicioWsaa.Url = argUrlWsaa
            If argProxy IsNot Nothing Then
                servicioWsaa.Proxy = New WebProxy(argProxy, True)
                If argProxyUser IsNot Nothing Then
                    Dim Credentials As New NetworkCredential(argProxyUser, argProxyPassword)
                    servicioWsaa.Proxy.Credentials = Credentials
                End If
            End If
            loginTicketResponse = servicioWsaa.loginCms(cmsFirmadoBase64)
            MsgBox(loginTicketResponse)
        Catch excepcionAlInvocarWsaa As Exception
            Throw New Exception("***Error INVOCANDO al servicio WSAA : " + excepcionAlInvocarWsaa.Message)
        End Try
        ' PASO 4: Analizo el Login Ticket Response recibido del WSAA
        Try
            XmlLoginTicketResponse = New XmlDocument()
            XmlLoginTicketResponse.LoadXml(loginTicketResponse)
            Me.UniqueId = UInt32.Parse(XmlLoginTicketResponse.SelectSingleNode("//uniqueId").InnerText)
            Me.GenerationTime = DateTime.Parse(XmlLoginTicketResponse.SelectSingleNode("//generationTime").InnerText)
            Me.ExpirationTime = DateTime.Parse(XmlLoginTicketResponse.SelectSingleNode("//expirationTime").InnerText)
            Me.Sign = XmlLoginTicketResponse.SelectSingleNode("//sign").InnerText
            Me.Token = XmlLoginTicketResponse.SelectSingleNode("//token").InnerText
        Catch excepcionAlAnalizarLoginTicketResponse As Exception
            Throw New Exception("***Error ANALIZANDO el LoginTicketResponse : " + excepcionAlAnalizarLoginTicketResponse.Message)
        End Try
        Return loginTicketResponse
    End Function

End Class
