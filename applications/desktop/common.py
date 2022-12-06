def build_index_html(remote_url):
    index_html_content = f'''
<!DOCTYPE html>
<html>
<body>
    <iframe src="{remote_url}" style="position:fixed; top:0; left:0; bottom:0; right:0; width:100%; height:100%; border:none; margin:0; padding:0; overflow:hidden; z-index:999999;"></iframe>
</body>
</html>
    '''
    with open('index.html', 'w') as f:
        print(index_html_content, file=f)
