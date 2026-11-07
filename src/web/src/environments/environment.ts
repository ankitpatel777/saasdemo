// Production settings. Placeholders only — this repo is public; real values are injected at deploy time.
export const environment = {
  production: true,
  apiBaseUrl: '<api-base-url>',
  msal: {
    clientId: '<spa-client-id>',
    authority: 'https://login.microsoftonline.com/organizations',
    apiScope: 'api://<api-client-id>/access_as_user',
  },
};
