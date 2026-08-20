// Production environment — API URL is injected at Render build time.
//
// In the Render Static Site settings, set these:
//
//   Environment variable:
//     API_URL = https://<your-api-service>.onrender.com/api
//
//   Build command:
//     npm ci && sed -i "s|ONENEST_API_URL_PLACEHOLDER|${API_URL}|g" \
//       src/environments/environment.prod.ts && ng build
//
// The sed command replaces the placeholder below with the real API URL before
// the Angular compiler processes this file.  The compiled output will contain
// the actual URL — this file is never deployed as-is.

export const environment = {
  production: true,
  apiBaseUrl: 'ONENEST_API_URL_PLACEHOLDER'
};
