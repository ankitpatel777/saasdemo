// Local development settings. Placeholders only — never commit real IDs.
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7294',
  msal: {
    clientId: '<spa-client-id>',
    authority: 'https://login.microsoftonline.com/organizations',
    apiScope: 'api://<api-client-id>/access_as_user',
  },
};
