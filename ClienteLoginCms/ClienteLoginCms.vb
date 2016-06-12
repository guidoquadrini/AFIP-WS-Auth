Imports System.Xml
Imports System.Security
Imports System.IO
Imports LibGenerales

Public Class ProgramaPrincipal
    Property strUrlWsaaWsdl As String
    Property strUrlServicio As String
    Property strIdServicioNegocio As String
    Property strRutaCertSigner As String
    Property rutaTicketAcceso As String
    Property strPasswordNonSecureString As String = String.Empty
    Property strPasswordSecureString As New SecureString
    Property strProxy As String
    Property strProxyUser As String
    Property strProxyPassword As String
    Property blnVerboseMode As Boolean
    Private Property RutaCertificado As String
    Private Property RutaBinWSAA As String
    Private Property MetodoGeneracionDeTicket As String
    Dim _ModoProduccion As Boolean
    Property ModoProduccion As Boolean
        Get
            Return _ModoProduccion
        End Get
        Set(value As Boolean)
            _ModoProduccion = value
            If value Then
                strUrlServicio = RegEdit.ObtenerRegistro(eCategorias.WSAA, "URLWSAA_Produccion")
            Else
                strUrlServicio = RegEdit.ObtenerRegistro(eCategorias.WSAA, "URLWSAA_Testing")
            End If
            strUrlWsaaWsdl = strUrlServicio & "?WSDL"
        End Set
    End Property

    Public Sub New()
        Try
            Dim directorio As String = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_TA_PATH")
            If Directory.Exists(directorio) = False Then ' si no existe la carpeta se crea
                Directory.CreateDirectory(directorio)
            End If
            rutaTicketAcceso = directorio & "TA.xml"
            MetodoGeneracionDeTicket = RegEdit.ObtenerRegistro(eCategorias.WSAA, "MetodoGeneracionDeTicket")
            RutaCertificado = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_CERTSIGNER")
            RutaBinWSAA = RegEdit.ObtenerRegistro(eCategorias.WSAA, "RutaBinario")
            ModoProduccion = RegEdit.ObtenerRegistro(eCategorias.WSAA, "ModoProduccion")
            strIdServicioNegocio = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_SERVICIO")
            strRutaCertSigner = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_CERTSIGNER")
            strProxy = IIf(RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY") = "", Nothing, RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY"))
            strProxyUser = IIf(RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY_USER") = "", Nothing, RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY_USER"))
            strProxyPassword = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY_PASSWORD")
            blnVerboseMode = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_VERBOSE")
            Dim strPasswordSecureString As New SecureString
            CargarTicketDeAcceso()
        Catch ex As Exception
            Throw New Exception(ex.Message, ex.InnerException)
            'TODO: Quizas aqui deberia revisar como suministrar mejor los errores.
        End Try
    End Sub

#Region "Carga, generacion y construccion del Ticket de Acceso"
    Public Function CargarTicketDeAcceso() As LoginTicket
        Dim TicketDeAcceso As LoginTicket
        TicketDeAcceso = BuscarTicketNoVencido()
        If TicketDeAcceso Is Nothing Then
            TicketDeAcceso = GenerarTicketDeAcceso(MetodoGeneracionDeTicket)
        End If
        Return TicketDeAcceso
    End Function

    Private Function BuscarTicketNoVencido() As LoginTicket
        Dim vRet As LoginTicket
        vRet = LeerTicketDeAcceso()
        If vRet Is Nothing Then Return Nothing
        If vRet.ExpirationTime < DateTime.Now Then Return Nothing
        Return vRet
    End Function

    Private Function GenerarTicketDeAcceso(ByVal pMetodo As String) As LoginTicket
        Dim Ticket As LoginTicket
        Select Case pMetodo
            Case "Proyecto"
                Ticket = GenerarTicketDeAccesoProyecto()
            Case "Binario"
                Ticket = GenerarTicketDeAccesoBinario()
            Case Else
                Throw New Exception("El metodo de generacion de ticket es desconocido para el sistema.")
        End Select
        Return Ticket
    End Function

    Private Function GenerarTicketDeAccesoProyecto() As LoginTicket
        Dim objTicketRespuesta As LoginTicket
        Dim strTicketRespuesta As String
        Try
            objTicketRespuesta = New LoginTicket()
            strTicketRespuesta = objTicketRespuesta.ObtenerLoginTicketResponse( _
                strIdServicioNegocio, strUrlWsaaWsdl, strRutaCertSigner, strPasswordSecureString, _
                strProxy, strProxyUser, strProxyPassword, blnVerboseMode)
        Catch excepcionAlObtenerTicket As Exception
            Throw New Exception("Error al solicitar Ticket de Acceso. " + excepcionAlObtenerTicket.Message)
        End Try
        GuardarTicketDeAcceso(strTicketRespuesta)
        Return objTicketRespuesta
    End Function

    Private Function GenerarTicketDeAccesoBinario() As LoginTicket
        Dim wTicket As LoginTicket = Nothing
        Try
            Dim oProcess As New Process()
            Dim oStartInfo As New ProcessStartInfo(RutaBinWSAA, "-c " & RutaCertificado & " -w " & strUrlServicio)
            oStartInfo.UseShellExecute = False
            oStartInfo.RedirectStandardOutput = True
            oProcess.StartInfo = oStartInfo
            oProcess.Start()
            Dim sOutput As String
            Using oStreamReader As System.IO.StreamReader = oProcess.StandardOutput
                sOutput = oStreamReader.ReadToEnd()
            End Using
            GuardarTicketDeAcceso(sOutput)
            wTicket = New LoginTicket(LeerXML(rutaTicketAcceso), rutaTicketAcceso)
        Catch ex As Exception
            Throw New Exception(ex.Message, ex.InnerException)
        End Try
        Return wTicket
    End Function

    Private Sub GuardarTicketDeAcceso(ByVal pTicket As String)
        Try
            If File.Exists(rutaTicketAcceso) Then File.Delete(rutaTicketAcceso)
            Dim escritor As StreamWriter
            escritor = File.AppendText(rutaTicketAcceso)
            escritor.Write(pTicket)
            escritor.Flush()
            escritor.Close()
        Catch ex As Exception
            Throw New Exception("Escritura realizada incorrectamente. No se pudo guardar el Ticket de Acceso en el disco.")
        End Try
    End Sub

    Private Function LeerTicketDeAcceso() As LoginTicket
        Dim vRet As LoginTicket
        Try
            'Determina si el Archivo existe.
            Dim ruta As String = RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_TA_PATH") & "TA.xml"
            If My.Computer.FileSystem.FileExists(ruta) Then
                Dim ArchivoXML As XmlDocument = LeerXML(ruta)
                vRet = New LoginTicket(ArchivoXML, ruta)
            Else
                vRet = Nothing
            End If
        Catch ex As Exception
            Throw New Exception(ex.Message, ex.InnerException)
        End Try
        Return vRet
    End Function

    Private Function LeerXML(ruta As String) As XmlDocument
        Dim vRet As New XmlDocument
        Try
            vRet.Load(ruta)
        Catch ex As Exception
            Throw New Exception("No se pudo cargar el archivo XML de la ruta especificada.")
        End Try
        Return vRet
    End Function
#End Region

    Public Shared Function ObtenerAppConfig() As DataTable
        Dim vRet As New DataTable
        vRet.Columns.Add("Clave")
        vRet.Columns.Add("Valor")
        vRet.Rows.Add({"ClienteLoginCms_Wsaa_LoginCMSService", RegEdit.ObtenerRegistro(eCategorias.WSAA, "ClienteLoginCms_Wsaa_LoginCMSService")})
        vRet.Rows.Add({"DEFAULT_CERTSIGNER", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_CERTSIGNER")})
        vRet.Rows.Add({"DEFAULT_PROXY", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY")})
        vRet.Rows.Add({"DEFAULT_PROXY_PASSWORD", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY_PASSWORD")})
        vRet.Rows.Add({"DEFAULT_PROXY_USER", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_PROXY_USER")})
        vRet.Rows.Add({"DEFAULT_SERVICIO", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_SERVICIO")})
        vRet.Rows.Add({"DEFAULT_TA_PATH", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_TA_PATH")})
        vRet.Rows.Add({"DEFAULT_URLWSAAWSDL", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_URLWSAAWSDL")})
        vRet.Rows.Add({"DEFAULT_VERBOSE", RegEdit.ObtenerRegistro(eCategorias.WSAA, "DEFAULT_VERBOSE")})
        vRet.Rows.Add({"MetodoGeneracionDeTicket", RegEdit.ObtenerRegistro(eCategorias.WSAA, "MetodoGeneracionDeTicket")})
        vRet.Rows.Add({"ModoProduccion", RegEdit.ObtenerRegistro(eCategorias.WSAA, "ModoProduccion")})
        vRet.Rows.Add({"RutaBinario", RegEdit.ObtenerRegistro(eCategorias.WSAA, "RutaBinario")})
        vRet.Rows.Add({"URLWSAA_Produccion", RegEdit.ObtenerRegistro(eCategorias.WSAA, "URLWSAA_Produccion")})
        vRet.Rows.Add({"URLWSAA_Testing", RegEdit.ObtenerRegistro(eCategorias.WSAA, "URLWSAA_Testing")})

        Return vRet
    End Function
End Class