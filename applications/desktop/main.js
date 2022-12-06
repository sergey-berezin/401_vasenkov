const {app, BrowserWindow} = require('electron')
const path = require('path')

function createWindow () {
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      webPreferences: {webviewTag: true}
    }
  });

  mainWindow.loadFile('index.html');

  mainWindow.on('close', e => {
    mainWindow.destroy()
    app.quit()
  });

  //mainWindow.webContents.openDevTools();
}

function Sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

app.commandLine.appendSwitch('ignore-certificate-errors')

app.whenReady().then(() => {
  createWindow()

  app.on('activate', function () {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') app.quit()
})
