// Production settings. The SPA client id and the API scope are PUBLIC identifiers (the same app
// registrations serve local and production). apiBaseUrl is injected by the CI/CD pipeline.
export const environment = {
  production: true,
  apiBaseUrl: '<api-base-url>',
  msal: {
    clientId: 'b0e6daa9-320c-4aa6-b869-38e2ed0f7bb7', // saasdemo-spa (multi-tenant)
    authority: 'https://login.microsoftonline.com/organizations',
    apiScope: 'api://258391af-5101-4686-aa05-6033a4475f57/access_as_user',
  },
};
