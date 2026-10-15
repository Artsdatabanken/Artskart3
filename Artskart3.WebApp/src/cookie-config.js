const cookieConfigs = {
  localhost: 'artskart3.test.artsdatabanken.no',
  '127.0.0.1': 'artskart3.test.artsdatabanken.no',
  'artskart3.test.artsdatabanken.no': 'artskart3.test.artsdatabanken.no',
  'artskart3.artsdatabanken.no': 'artskart3.artsdatabanken.no',
};

const cookieConfig = cookieConfigs[location.hostname];
if (cookieConfig) {
  const cookieConfigScript = document.createElement('script');
  cookieConfigScript.src = `https://policy.app.cookieinformation.com/latest/${cookieConfig}/nb.js`;
  cookieConfigScript.charset = 'UTF-8';
  document.head.appendChild(cookieConfigScript);
}
