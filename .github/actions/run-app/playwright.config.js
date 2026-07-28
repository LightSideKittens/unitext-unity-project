module.exports = {
  use: { ignoreHTTPSErrors: true },
  timeout: parseInt(process.env.APP_TIMEOUT || '1800') * 1000,
};
