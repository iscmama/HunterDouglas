using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.ServiceModel.Description;
using System.Web.Http;

namespace PruebaWebApp1.Controllers
{
    [RoutePrefix("api/Procesos")]
    public class ProcesosController : ApiController
    {
        [HttpPost]        
        [Route("AutenticarUsuario")]
        public IHttpActionResult AutenticarUsuario(UsuarioBO modelo)
        {
            List<CitaBO> lista = new List<CitaBO>();
            bool autenticado = false;
            string mensaje = string.Empty;

            try
            {
                Guid userId = Guid.Empty;
                string crm_url_connection = string.Empty, crm_user = modelo.Usuario, crm_password = modelo.Password;
                crm_url_connection = ConfigurationManager.AppSettings["CRM_URL_CONNECTION"];
                var connection = ToolsCRM.getService(crm_url_connection, crm_user, crm_password, out userId);
                if (connection != null)
                {
                    QueryExpression queryExpression = new QueryExpression("appointment");
                    queryExpression.ColumnSet = new ColumnSet(true);
                    queryExpression.Criteria.AddCondition("ownerid", ConditionOperator.Equal, userId);
                    queryExpression.Criteria.AddCondition("statecode", ConditionOperator.Equal, 3);
                    EntityCollection results = connection.RetrieveMultiple(queryExpression);
                    if (results != null && results.Entities != null && results.Entities.Any())
                    {
                        foreach (var entity in results.Entities)
                        {
                            DateTime horallegada = DateTime.MinValue, horasalida = DateTime.MinValue, fechaInicioProgramado = DateTime.MinValue;
                            double latitudInicio = 0, longitudinicio = 0, latitudfin = 0, longitudfin = 0;

                            if (entity.Attributes.Contains("scheduledstart"))
                            {
                                fechaInicioProgramado = entity.GetAttributeValue<DateTime>("scheduledstart");
                            }

                            if (entity.Attributes.Contains("hd_inicio_real"))
                            {
                                horallegada = entity.GetAttributeValue<DateTime>("hd_inicio_real");
                            }

                            if (entity.Attributes.Contains("hd_fin_real"))
                            {
                                horasalida = entity.GetAttributeValue<DateTime>("hd_fin_real");
                            }

                            if (entity.Attributes.Contains("hd_latitud_in"))
                            {
                                latitudInicio = entity.GetAttributeValue<double>("hd_latitud_in");
                            }

                            if (entity.Attributes.Contains("hd_latitud_out"))
                            {
                                latitudfin = entity.GetAttributeValue<double>("hd_latitud_out");
                            }

                            if (entity.Attributes.Contains("hd_longitud_in"))
                            {
                                longitudinicio = entity.GetAttributeValue<double>("hd_longitud_in");
                            }

                            if (entity.Attributes.Contains("hd_longitud_out"))
                            {
                                longitudfin = entity.GetAttributeValue<double>("hd_longitud_out");
                            }

                            string tema = string.Empty, tipoSeguimiento = string.Empty;

                            if (entity.Attributes.Contains("subject"))
                            {
                                tema = entity.Attributes["subject"].ToString();
                            }

                            if (entity.FormattedValues.Contains("hd_tipo_seguimiento"))
                            {
                                tipoSeguimiento = entity.FormattedValues["hd_tipo_seguimiento"];
                            }

                            lista.Add(new CitaBO
                            {
                                Id = entity.Id,
                                //Tema = string.Format("{0} - {1}", tema, fechaInicioProgramado.ToString("dd/MM/yyyy")),
                                Tema = tema,
                                StrTipoSeguimiento = tipoSeguimiento,
                                Usuario = modelo.Usuario,
                                Password = modelo.Password,
                                HoraLlegada = horallegada == DateTime.MinValue ? string.Empty : horallegada.ToString("dd/MM/yyyy HH:mm"),
                                HoraSalida = horasalida == DateTime.MinValue ? string.Empty : horasalida.ToString("dd/MM/yyyy HH:mm"),
                                LatitudFin = latitudfin,
                                LatitudInicio = latitudInicio,
                                LongitudFin = longitudfin,
                                LongitudInicio = longitudinicio,
                                FechaInicioProgramado = fechaInicioProgramado
                            });
                        }
                    }
                    autenticado = true;

                    if (lista.Any())
                    {
                        lista = lista.OrderBy(p => p.FechaInicioProgramado).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
                autenticado = false;
            }

            return Ok(new { lista, autenticado, mensaje });
        }

        [HttpPost]
        [Route("RegistrarCheckIn")]
        public IHttpActionResult RegistrarCheckIn(CitaBO modelo)
        {
            string mensaje = string.Empty;

            try
            {
                Guid userId = Guid.Empty;
                string crm_url_connection = string.Empty, crm_user = modelo.Usuario, crm_password = modelo.Password;
                crm_url_connection = ConfigurationManager.AppSettings["CRM_URL_CONNECTION"];
                var connection = ToolsCRM.getService(crm_url_connection, crm_user, crm_password, out userId);
                if (connection != null)
                {
                    bool realizaProceso = true;
                    Entity currentEntity = connection.Retrieve("appointment", modelo.Id, new ColumnSet(true));
                    if (currentEntity != null)
                    {
                        if (currentEntity.Attributes.Contains("hd_latitud_in"))
                        {
                            double existeLatitud = currentEntity.GetAttributeValue<double>("hd_latitud_in");
                            if (existeLatitud != 0)
                            {
                                if (currentEntity.Attributes.Contains("hd_validacion_check"))
                                {
                                    int existeContador = currentEntity.GetAttributeValue<int>("hd_validacion_check");
                                    existeContador = existeContador + 1;
                                    Entity entityToUpdate = new Entity("appointment");
                                    entityToUpdate.Id = currentEntity.Id;
                                    entityToUpdate.Attributes = new AttributeCollection();
                                    entityToUpdate.Attributes.Add("hd_validacion_check", existeContador);
                                    connection.Update(entityToUpdate);
                                }
                                else
                                {
                                    int existeContador = 1;
                                    Entity entityToUpdate = new Entity("appointment");
                                    entityToUpdate.Id = currentEntity.Id;
                                    entityToUpdate.Attributes = new AttributeCollection();
                                    entityToUpdate.Attributes.Add("hd_validacion_check", existeContador);
                                    connection.Update(entityToUpdate);
                                }

                                return Ok(mensaje);
                            }
                        }

                        var statuscode = currentEntity.GetAttributeValue<OptionSetValue>("statuscode");
                        //if (statuscode.Value == 100010002)
                        if (statuscode.Value == 5)
                        {
                            #region cita programada
                            string tipoDeCita = currentEntity.FormattedValues["hd_tipo_seguimiento"];
                            if (tipoDeCita.ToLower().Contains("prospección") && !currentEntity.Attributes.Contains("hd_prospeccionid"))
                            {
                                realizaProceso = false;
                                mensaje = "La cita debe de tener prospección seleccionada.";
                            }

                            #region seguimiento y cliente final
                            if (realizaProceso && (tipoDeCita.ToLower().Contains("cliente final") || tipoDeCita.ToLower().Contains("seguimiento")))
                            {
                                bool existeRegistroDeLlamada = false;
                                QueryExpression queryExpression2 = new QueryExpression("phonecall");
                                queryExpression2.ColumnSet = new ColumnSet(true);
                                queryExpression2.Criteria.AddCondition("hd_citallamadaid", ConditionOperator.Equal, currentEntity.Id);
                                EntityCollection results2 = connection.RetrieveMultiple(queryExpression2);
                                if (results2 != null && results2.Entities != null && results2.Entities.Count > 0)
                                {
                                    existeRegistroDeLlamada = true;
                                }

                                if (!existeRegistroDeLlamada)
                                {
                                    realizaProceso = false;
                                    mensaje = "La cita debe de tener al menos un registro de llamada.";
                                }
                            }
                            #endregion

                            #region prospección
                            if (realizaProceso && tipoDeCita.ToLower().Contains("prospección") && currentEntity.Attributes.Contains("hd_contacto_visita"))
                            {
                                string aux = currentEntity.FormattedValues["hd_contacto_visita"].ToLower();
                                if (aux == "si" || aux == "sí")
                                {
                                    bool existeRegistroDeLlamada = false;
                                    QueryExpression queryExpression2 = new QueryExpression("phonecall");
                                    queryExpression2.ColumnSet = new ColumnSet(true);
                                    queryExpression2.Criteria.AddCondition("hd_citallamadaid", ConditionOperator.Equal, currentEntity.Id);
                                    EntityCollection results2 = connection.RetrieveMultiple(queryExpression2);
                                    if (results2 != null && results2.Entities != null && results2.Entities.Count > 0)
                                    {
                                        existeRegistroDeLlamada = true;
                                    }

                                    if (!existeRegistroDeLlamada)
                                    {
                                        realizaProceso = false;
                                        mensaje = "La cita debe de tener al menos un registro de llamada.";
                                    }
                                }
                            }
                            #endregion
                            #endregion
                        }
                        else
                        {
                            realizaProceso = false;
                            mensaje = "La cita no ha sido aprobada por su gerente";
                        }
                    }

                    if (realizaProceso)
                    {
                        DateTime dtAux = new DateTime(modelo.year, modelo.month, modelo.day, modelo.hour, modelo.minute, 0);
                        Entity entityToUpdate = new Entity("appointment");
                        entityToUpdate.Id = modelo.Id;
                        entityToUpdate.Attributes = new AttributeCollection();
                        entityToUpdate.Attributes.Add("hd_inicio_real", dtAux);
                        entityToUpdate.Attributes.Add("hd_latitud_in", modelo.LatitudInicio);
                        entityToUpdate.Attributes.Add("hd_longitud_in", modelo.LongitudInicio);
                        connection.Update(entityToUpdate);
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = "Excepción:" + ex.Message;
            }

            return Ok(mensaje);
        }

        [HttpPost]
        [Route("RegistrarCheckOut")]
        public IHttpActionResult RegistrarCheckOut(CitaBO modelo)
        {
            string mensaje = string.Empty;

            try
            {
                Guid userId = Guid.Empty;
                string crm_url_connection = string.Empty, crm_user = modelo.Usuario, crm_password = modelo.Password;
                crm_url_connection = ConfigurationManager.AppSettings["CRM_URL_CONNECTION"];
                var connection = ToolsCRM.getService(crm_url_connection, crm_user, crm_password, out userId);
                if (connection != null)
                {
                    Entity currentEntity = connection.Retrieve("appointment", modelo.Id, new ColumnSet(true));
                    if (currentEntity != null)
                    {
                        if (currentEntity.Attributes.Contains("hd_latitud_out"))
                        {
                            double existeLatitud = currentEntity.GetAttributeValue<double>("hd_latitud_out");
                            if (existeLatitud != 0)
                            {
                                return Ok(mensaje);
                            }
                        }

                        bool realizaProceso = true;
                        string tipoDeCita = currentEntity.FormattedValues["hd_tipo_seguimiento"];

                        #region Seguimiento
                        if (!string.IsNullOrEmpty(tipoDeCita) && tipoDeCita.ToLower().Contains("seguimiento"))
                        {
                            if (currentEntity.Attributes.Contains("hd_distribuidorid"))
                            {
                                QueryExpression queryExpression = new QueryExpression("hd_matriz_seguimiento");
                                queryExpression.ColumnSet = new ColumnSet(true);
                                queryExpression.Criteria.AddCondition("hd_citasid", ConditionOperator.Equal, modelo.Id);
                                EntityCollection results = connection.RetrieveMultiple(queryExpression);
                                if (results != null && results.Entities != null && results.Entities.Any())
                                {
                                    foreach (var aux in results.Entities)
                                    {
                                        bool tienePrioridad = aux.GetAttributeValue<bool>("hd_prioridad");
                                        if (tienePrioridad)
                                        {
                                            if (!aux.Attributes.Contains("hd_punto_revisado"))
                                            {
                                                realizaProceso = false;
                                                mensaje = "Se deben revisar todos los puntos con prioridad para esta visita. Revisar la matriz de seguimiento.";
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                realizaProceso = false;
                                mensaje = "La cita debe de tener un distribuidor seleccionado.";
                            }
                        }
                        #endregion

                        #region Prospeccion
                        if (!string.IsNullOrEmpty(tipoDeCita) && tipoDeCita.ToLower().Contains("prospección"))
                        {
                            if (currentEntity.Attributes.Contains("hd_prospeccionid"))
                            {
                                EntityReference refProspeccion = currentEntity.GetAttributeValue<EntityReference>("hd_prospeccionid");
                                if (refProspeccion != null)
                                {
                                    string fase = string.Empty, faseActual = string.Empty;
                                    bool checklistCompleto = false;

                                    Entity entityProspeccion = connection.Retrieve(refProspeccion.LogicalName, refProspeccion.Id, new ColumnSet(true));
                                    if (entityProspeccion.Attributes.Contains("hd_fase"))
                                    {
                                        fase = entityProspeccion.FormattedValues["hd_fase"];
                                    }

                                    if (entityProspeccion.Attributes.Contains("hd_fase_actual"))
                                    {
                                        faseActual = entityProspeccion.FormattedValues["hd_fase_actual"];
                                    }

                                    if (entityProspeccion.Attributes.Contains("hd_checklist_completado"))
                                    {
                                        checklistCompleto = entityProspeccion.GetAttributeValue<bool>("hd_checklist_completado");
                                    }

                                    if (!checklistCompleto)
                                    {
                                        if (fase.ToLower() != "cita reprogramada")
                                        {
                                            if (fase.ToLower() == faseActual.ToLower())
                                            {
                                                realizaProceso = false;
                                                mensaje = "Para realizar check-out, debe cumplir con al menos una fase del proceso. En caso de no contar con la información, reprogramar la cita.";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        #endregion

                        if (realizaProceso)
                        {
                            DateTime dtAux = new DateTime(modelo.year, modelo.month, modelo.day, modelo.hour, modelo.minute, 0);
                            Entity entityToUpdate = new Entity("appointment");
                            entityToUpdate.Id = modelo.Id;
                            entityToUpdate.Attributes = new AttributeCollection();
                            entityToUpdate.Attributes.Add("hd_fin_real", dtAux);
                            entityToUpdate.Attributes.Add("hd_latitud_out", modelo.LatitudFin);
                            entityToUpdate.Attributes.Add("hd_longitud_out", modelo.LongitudFin);
                            connection.Update(entityToUpdate);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
            }

            return Ok(mensaje);
        }
    }

    public static class ToolsCRM
    {
        public static IOrganizationService getService(string url, string user, string password, out Guid userId)
        {
            IOrganizationService _service;
            OrganizationServiceContext _servicecontext;
            Uri OrganizationUri = new Uri(url);
            var Credentials = new ClientCredentials();
            Credentials.UserName.UserName = user;
            Credentials.UserName.Password = password;
            OrganizationServiceProxy _serviceProxy = new OrganizationServiceProxy(OrganizationUri, null, Credentials, null);
            _serviceProxy.ServiceConfiguration.CurrentServiceEndpoint.Behaviors.Add(new ProxyTypesBehavior());
            _service = (IOrganizationService)_serviceProxy;
            _servicecontext = new OrganizationServiceContext(_service);
            userId = ((WhoAmIResponse)_service.Execute(new WhoAmIRequest())).UserId;
            return _service;
        }
    }

    public class UsuarioBO
    {
        public string Usuario { get; set; }
        public string Password { get; set; }
    }

    public class CitaBO : UsuarioBO
    {
        public Guid Id { get; set; }
        public string Tema { get; set; }
        public string StrTipoSeguimiento { get; set; }
        public int TipoSeguimiento { get; set; }
        public string HoraLlegada { get; set; }
        public string HoraSalida { get; set; }
        public double LongitudInicio { get; set; }
        public double LongitudFin { get; set; }
        public double LatitudInicio { get; set; }
        public double LatitudFin { get; set; }

        public int year { get; set; }
        public int month { get; set; }
        public int day { get; set; }
        public int hour { get; set; }
        public int minute { get; set; }

        public DateTime FechaInicioProgramado { get; set; }
    }
}
