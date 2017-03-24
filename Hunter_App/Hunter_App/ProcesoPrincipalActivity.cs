using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Plugin.Geolocator;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Http;
using Android.Net;
using Android.Util;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;

namespace Hunter_App
{
    [Activity(Label = "Hunter Douglas App", Icon = "@drawable/icon", NoHistory =true)]
    public class ProcesoPrincipalActivity : Activity //, ILocationListener
    {
        string urlWebAPI = "http://138.0.216.3/WebAPI/";//Production
        //string urlWebAPI = "http://138.0.216.3/WebAPITest/";//Test
        ProgressDialog progress;
        List<CitaBO> citas = new List<CitaBO>();
        CitaBO citaSeleccionada = null;
        int position = 0;
        string usuarioinformacion = string.Empty;
        static readonly string TAG = typeof(ProcesoPrincipalActivity).FullName;
        Context context = Application.Context;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.ProcesoPrincipalActivity);

            string contenido = Intent.GetStringExtra("contenido") ?? string.Empty;

            if (!string.IsNullOrEmpty(contenido))
            {
                AutenticarUsuarioResult resultado = Newtonsoft.Json.JsonConvert.DeserializeObject<AutenticarUsuarioResult>(contenido);
                citas = resultado.lista;

                if (citas != null && citas.Any())
                {
                    UsuarioBO usuario = new UsuarioBO { Usuario = citas[0].Usuario, Password = citas[0].Password };
                    usuarioinformacion = Newtonsoft.Json.JsonConvert.SerializeObject(usuario);
                }
            }

            Spinner spinner = FindViewById<Spinner>(Resource.Id.spinner1);
            spinner.Adapter = new CitasAdapter(this, citas);
            spinner.ItemSelected += new EventHandler<AdapterView.ItemSelectedEventArgs>(spinner_ItemSelected);

            Button btnCheckin = FindViewById<Button>(Resource.Id.button1);
            Button btnCheckout = FindViewById<Button>(Resource.Id.button2);
            Button btnCrm = FindViewById<Button>(Resource.Id.button3);

            #region btnCheckIn
            btnCheckin.Click += delegate
            {
                try
                {
                    if (!GetIsInternetAccessAvailable())
                    {
                        Toast.MakeText(context, "Sin Acceso a Internet. Verifique", ToastLength.Long).Show();
                        return;
                    }

                    if (!IsHostReachable())
                    {
                        Toast.MakeText(context, "Sin comunicación con el Servicio. Verifique", ToastLength.Long).Show();
                        return;
                    }

                    if (citaSeleccionada != null)
                    {
                        progress = new ProgressDialog(this);
                        progress.Indeterminate = true;
                        progress.SetProgressStyle(ProgressDialogStyle.Spinner);
                        progress.SetMessage("Realizando Check-In...");
                        progress.SetCancelable(false);
                        progress.Show();

                        new Thread(new ThreadStart(async delegate
                        {
                            try
                            {
                                #region realiza proceso
                                var locator = CrossGeolocator.Current;
                                locator.DesiredAccuracy = 1;
                                var position = await locator.GetPositionAsync(timeoutMilliseconds: 10000);
                                if (position != null)
                                {
                                    DateTime momento = DateTime.Now;

                                    citaSeleccionada.LatitudInicio = position.Latitude;
                                    citaSeleccionada.LongitudInicio = position.Longitude;
                                    citaSeleccionada.HoraLlegada = momento.ToString("dd/MM/yyyy HH:mm");
                                    citaSeleccionada.year = momento.Year;
                                    citaSeleccionada.month = momento.Month;
                                    citaSeleccionada.day = momento.Day;
                                    citaSeleccionada.hour = momento.Hour;
                                    citaSeleccionada.minute = momento.Minute;

                                    string resultado = await DoCheckInAsync();

                                    if (string.IsNullOrEmpty(resultado))
                                    {
                                        RunOnUiThread(() =>
                                        {
                                            progress.Hide();
                                            progress.Dispose();
                                            CargaInformacionConResultado("CI");
                                        });
                                    }
                                    else
                                    {
                                        RunOnUiThread(() =>
                                        {
                                            citaSeleccionada.LatitudInicio = 0;
                                            citaSeleccionada.LongitudInicio = 0;
                                            citaSeleccionada.HoraLlegada = string.Empty;
                                            citaSeleccionada.year = 0;
                                            citaSeleccionada.month = 0;
                                            citaSeleccionada.day = 0;
                                            citaSeleccionada.hour = 0;
                                            citaSeleccionada.minute = 0;

                                            progress.Hide();
                                            progress.Dispose();
                                            AlertDialog.Builder alert = new AlertDialog.Builder(this);
                                            alert.SetTitle("Información");
                                            alert.SetMessage(resultado);
                                            alert.SetPositiveButton("Aceptar", (senderAlert, args) => { });
                                            Dialog dialog = alert.Create();
                                            dialog.Show();
                                        });
                                    }
                                }
                                #endregion
                            }
                            catch (Exception ex)
                            {
                                Toast.MakeText(this, "Mensaje error thread:" + ex.Message, ToastLength.Long).Show();
                            }
                        })).Start();
                    }
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, "Mensaje error clickCheckIn:" + ex.Message, ToastLength.Long).Show();
                }
            };
            #endregion

            #region btnCheckout

            btnCheckout.Click += delegate
            {
                try
                {
                    if (!GetIsInternetAccessAvailable())
                    {
                        Toast.MakeText(context, "Sin Acceso a Internet. Verifique", ToastLength.Long).Show();
                        return;
                    }

                    if (!IsHostReachable())
                    {
                        Toast.MakeText(context, "Sin comunicación con el Servicio. Verifique", ToastLength.Long).Show();
                        return;
                    }

                    if (citaSeleccionada != null)
                    {
                        progress = new ProgressDialog(this);
                        progress.Indeterminate = true;
                        progress.SetProgressStyle(ProgressDialogStyle.Spinner);
                        progress.SetMessage("Realizando Check-Out...");
                        progress.SetCancelable(false);
                        progress.Show();

                        new Thread(new ThreadStart(async delegate
                        {
                            var locator = CrossGeolocator.Current;
                            locator.DesiredAccuracy = 1;
                            var position = await locator.GetPositionAsync(timeoutMilliseconds: 10000);

                            if (position != null)
                            {
                                DateTime momento = DateTime.Now;

                                citaSeleccionada.LatitudFin = position.Latitude;
                                citaSeleccionada.LongitudFin = position.Longitude;
                                citaSeleccionada.HoraSalida = momento.ToString("dd/MM/yyyy HH:mm");
                                citaSeleccionada.year = momento.Year;
                                citaSeleccionada.month = momento.Month;
                                citaSeleccionada.day = momento.Day;
                                citaSeleccionada.hour = momento.Hour;
                                citaSeleccionada.minute = momento.Minute;

                                string resultado = await DoCheckOutAsync();

                                if (string.IsNullOrEmpty(resultado))
                                {
                                    RunOnUiThread(() =>
                                    {
                                        progress.Hide();
                                        progress.Dispose();
                                        CargaInformacionConResultado("CO");
                                    });
                                }
                                else
                                {
                                    RunOnUiThread(() =>
                                    {
                                        citaSeleccionada.LatitudFin = 0;
                                        citaSeleccionada.LongitudFin = 0;
                                        citaSeleccionada.HoraSalida = string.Empty;
                                        citaSeleccionada.year = 0;
                                        citaSeleccionada.month = 0;
                                        citaSeleccionada.day = 0;
                                        citaSeleccionada.hour = 0;
                                        citaSeleccionada.minute = 0;

                                        progress.Hide();
                                        progress.Dispose();
                                        AlertDialog.Builder alert = new AlertDialog.Builder(this);
                                        alert.SetTitle("Información");
                                        alert.SetMessage(resultado);
                                        alert.SetPositiveButton("Aceptar", (senderAlert, args) => { });
                                        Dialog dialog = alert.Create();
                                        dialog.Show();
                                    });
                                }
                            }
                        })).Start();
                    }
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, "Mensaje error clickCheckOut:" + ex.Message, ToastLength.Long).Show();
                }
            };
            #endregion

            #region btnCRM
            btnCrm.Click += delegate
            {
                try
                {
                    var existIntent = ApplicationContext.PackageManager.GetLaunchIntentForPackage("com.microsoft.crm.crmhost");
                    //var existIntent = ApplicationContext.PackageManager.GetLaunchIntentForPackage("com.whatsapp");
                    if (existIntent != null)
                    {
                        StartActivity(existIntent);
                    }
                    else
                    {
                        existIntent = ApplicationContext.PackageManager.GetLaunchIntentForPackage("com.microsoft.crm.crmphone");
                        if (existIntent != null)
                        {
                            StartActivity(existIntent);
                        }
                        else
                        {
                            var uri = Android.Net.Uri.Parse("market://details?id=com.microsoft.crm.crmhost");
                            var intent = new Intent(Intent.ActionView, uri);
                            StartActivity(intent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Toast.MakeText(this, "Mensaje error clickCRM:" + ex.Message, ToastLength.Long).Show();
                }
            };
            #endregion

            AndroidEnvironment.UnhandledExceptionRaiser += new EventHandler<RaiseThrowableEventArgs>(AndroidEnvironment_UnhandledExceptionRaiser);
        }

        void AndroidEnvironment_UnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            Toast.MakeText(this, "Mensaje error AndroidEnvironment_UnhandledExceptionRaiser:" + e.Exception.Message, ToastLength.Long).Show();
        }

        string DoCheckIn()
        {
            string mensaje = string.Empty;

            try
            {
                var client = new RestClient(urlWebAPI);
                var request = new RestRequest("api/Procesos/RegistrarCheckIn", Method.POST);
                request.AddObject(citaSeleccionada);
                IRestResponse response = client.Execute(request);
                if (!string.IsNullOrEmpty(response.Content))
                {
                    mensaje = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(response.Content);
                }
                else
                {
                    mensaje = "Revise su conexión a internet.";
                }
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
            }
            return mensaje;
        }

        string DoCheckOut()
        {
            string mensaje = string.Empty;

            try
            {
                var client = new RestClient(urlWebAPI);
                var request = new RestRequest("api/Procesos/RegistrarCheckOut", Method.POST);
                request.AddObject(citaSeleccionada);
                IRestResponse response = client.Execute(request);
                if (!string.IsNullOrEmpty(response.Content))
                {
                    mensaje = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(response.Content);
                }
                else
                {
                    mensaje = "Revise su conexión a internet.";
                }
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
            }
            return mensaje;
        }

        void CargaInformacionConResultado(string tipoMensajeFinal)
        {
            var progress2 = new ProgressDialog(this);
            progress2.Indeterminate = true;
            progress2.SetProgressStyle(ProgressDialogStyle.Spinner);
            progress2.SetMessage("Actualizando información...");
            progress2.SetCancelable(false);
            progress2.Show();

            new Thread(new ThreadStart(delegate
            {
                Thread.Sleep(2000);
                string resultadoMetodo = string.Empty;
                RunOnUiThread(() =>
                {
                    #region prueba
                    //progress2.Hide();
                    //progress2.Dispose();

                    //AlertDialog.Builder alert2 = new AlertDialog.Builder(this);
                    //alert2.SetTitle("Información");
                    //alert2.SetMessage(resultadoMetodo);
                    //alert2.SetPositiveButton("Cerrar", (senderAlert, args) =>
                    //{
                    //    var intent = new Intent(this, typeof(MainActivity));
                    //    intent.PutExtra("usuarioinformacion", usuarioinformacion);
                    //    RunOnUiThread(() =>
                    //    {
                    //        StartActivity(intent);
                    //    });
                    //});
                    //alert2.SetNegativeButton("SI", (senderAlert, args) =>
                    //{
                    //    CargaInformacionConResultado(tipoMensajeFinal);
                    //});
                    //Dialog dialog2 = alert2.Create();
                    //dialog2.Show();
                    #endregion

                    #region funciona
                    try
                    {
                        var client = new RestClient(urlWebAPI);
                        var request = new RestRequest("api/Procesos/AutenticarUsuario", Method.POST);
                        request.AddObject(new UsuarioBO { Usuario = citaSeleccionada.Usuario, Password = citaSeleccionada.Password });
                        IRestResponse response = client.Execute(request);
                        if (!string.IsNullOrEmpty(response.Content))
                        {
                            AutenticarUsuarioResult resultado = Newtonsoft.Json.JsonConvert.DeserializeObject<AutenticarUsuarioResult>(response.Content);
                            if (resultado != null)
                            {
                                citas = resultado.lista;
                                Spinner spinner = FindViewById<Spinner>(Resource.Id.spinner1);
                                spinner.Adapter = new CitasAdapter(this, citas);
                                //spinner.SetSelection(position);

                                if (tipoMensajeFinal == "CI")
                                {
                                    spinner.SetSelection(position);
                                    progress2.Hide();
                                    progress2.Dispose();

                                    AlertDialog.Builder alert2 = new AlertDialog.Builder(this);
                                    alert2.SetTitle("Información");
                                    alert2.SetMessage("Check-In realizado correctamente.");
                                    alert2.SetPositiveButton("Aceptar", (senderAlert, args) => { });
                                    Dialog dialog2 = alert2.Create();
                                    dialog2.Show();
                                }
                                else if (tipoMensajeFinal == "CO")
                                {
                                    progress2.Hide();
                                    progress2.Dispose();

                                    AlertDialog.Builder alert2 = new AlertDialog.Builder(this);
                                    alert2.SetTitle("Información");
                                    alert2.SetMessage("Check-Out realizado correctamente.");
                                    alert2.SetPositiveButton("Aceptar", (senderAlert, args) => { });
                                    Dialog dialog2 = alert2.Create();
                                    dialog2.Show();
                                }
                            }
                        }
                        else
                        {
                            resultadoMetodo = "Revise su conexión a internet, ¿desea volver a intentar cargar la información del CRM?";
                        }
                    }
                    catch (Exception ex)
                    {
                        resultadoMetodo = "El siguiente error se ha generado:" + ex.Message + " , ¿desea volver a intentar cargar la información del CRM?";
                    }

                    if (!string.IsNullOrEmpty(resultadoMetodo))
                    {
                        progress2.Hide();
                        progress2.Dispose();

                        AlertDialog.Builder alert2 = new AlertDialog.Builder(this);
                        alert2.SetTitle("Información");
                        alert2.SetMessage(resultadoMetodo);
                        alert2.SetPositiveButton("Cerrar", (senderAlert, args) =>
                        {
                            var intent = new Intent(this, typeof(MainActivity));
                            intent.PutExtra("usuarioinformacion", usuarioinformacion);
                            RunOnUiThread(() =>
                            {
                                StartActivity(intent);
                            });
                        });
                        alert2.SetNegativeButton("SI", (senderAlert, args) =>
                        {
                            CargaInformacionConResultado(tipoMensajeFinal);
                        });
                        Dialog dialog2 = alert2.Create();
                        dialog2.Show();
                    }
                    #endregion
                });
            })).Start();
        }

        private void spinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            try
            {
                Spinner spinner = (Spinner)sender;
                citaSeleccionada = citas[e.Position];
                position = e.Position;

                TextView textView = FindViewById<TextView>(Resource.Id.textView2);
                textView.Text = citaSeleccionada.StrTipoSeguimiento;

                Button btnCheckin = FindViewById<Button>(Resource.Id.button1);
                Button btnCheckout = FindViewById<Button>(Resource.Id.button2);

                btnCheckin.Enabled = false;
                btnCheckout.Enabled = false;
                if (citaSeleccionada.LatitudInicio == 0 || citaSeleccionada.LongitudInicio == 0 || string.IsNullOrEmpty(citaSeleccionada.HoraLlegada))
                {
                    btnCheckin.Enabled = true;
                }
                else
                {
                    if (citaSeleccionada.LatitudFin == 0 || citaSeleccionada.LongitudFin == 0 || string.IsNullOrEmpty(citaSeleccionada.HoraSalida))
                    {
                        btnCheckout.Enabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Mensaje error spinner_ItemSelected:" + ex.Message, ToastLength.Long).Show();
            }
        }

        public override void OnBackPressed()
        {
            //base.OnBackPressed();
        }

        #region Nueva Implementacion

        private bool GetIsInternetAccessAvailable()
        {
            try
            {
                ConnectivityManager connectivityManager = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);
                NetworkInfo networkInfo = connectivityManager.ActiveNetworkInfo;
                return networkInfo.IsConnected;
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, "Error en el metodo GetIsInternetAccessAvailable: {0}", ex.Message);
                return false;
            }
        }
        public bool IsHostReachable()
        {
            try
            {
                string CheckUrl = "http://138.0.216.3/WebAPI";
                HttpWebRequest iNetRequest = (HttpWebRequest)WebRequest.Create(CheckUrl);
                iNetRequest.Timeout = 3000;
                WebResponse iNetResponse = iNetRequest.GetResponse();
                iNetResponse.Close();

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, "Error en el metodo IsHostReachable: {0}", ex.Message);
                return false;
            }
        }
        private async Task<string> DoCheckInAsync()
        {
            string mensaje = string.Empty;

            try
            {
                string urlService = "http://138.0.216.3/WebAPI/api/Procesos/RegistrarCheckIn";//Production
                //string urlService = "http://138.0.216.3/WebAPITest/api/Procesos/RegistrarCheckIn";//Test

                var uri = new System.Uri(urlService);

                HttpClient client = new HttpClient();

                var json = JsonConvert.SerializeObject(citaSeleccionada);
                var contentParams = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(uri, contentParams);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    mensaje = JsonConvert.DeserializeObject<string>(content);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, "Error en el metodo DoCheckInAsync{0}", ex.Message);
                mensaje = "No se pudo realizar el Check In. Intente más tarde";
            }

            return mensaje;
        }
        private async Task<string> DoCheckOutAsync()
        {
            string mensaje = string.Empty;

            try
            {
                string urlService = "http://138.0.216.3/WebAPI/api/Procesos/RegistrarCheckOut";//Production
                //string urlService = "http://138.0.216.3/WebAPITest/api/Procesos/RegistrarCheckOut";//Test

                var uri = new System.Uri(urlService);

                HttpClient client = new HttpClient();

                var json = JsonConvert.SerializeObject(citaSeleccionada);
                var contentParams = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(uri, contentParams);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    mensaje = JsonConvert.DeserializeObject<string>(content);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, "Error en el metodo DoCheckOutAsync{0}", ex.Message);
                mensaje = "No se pudo realizar el Check Out. Intente más tarde";
            }

            return mensaje;
        }

        #endregion Nueva Implementacion
    }

    public class CitasAdapter : BaseAdapter<CitaBO>
    {
        private readonly Activity context;
        private readonly List<CitaBO> movies;

        public CitasAdapter(Activity context, List<CitaBO> movies)
        {
            this.context = context;
            this.movies = movies;
        }

        public override CitaBO this[int position]
        {
            get
            {
                return movies[position];
            }
        }

        public override int Count
        {
            get
            {
                return movies.Count;
            }
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = convertView;
            if (view == null) // otherwise create a new one
            {
                view = context.LayoutInflater.Inflate(Android.Resource.Layout.SimpleListItem1, null);
            }

            if (position>= 0 && movies.Count > position)
            {
                view.FindViewById<TextView>(Android.Resource.Id.Text1).Text = movies[position].ToString();
            }

            return view;
        }
    }
}