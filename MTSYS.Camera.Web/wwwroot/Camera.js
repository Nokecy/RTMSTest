/**
 * Wrap streamedian.js into extjs panel.
 * xuhy 2018.7.30 create.
 * TODO:from panel to component?
 */
Ext.define('Arim.view.camera.Camera', {
    extend: 'Ext.panel.Panel',
    xtype: 'cameraplayer',

    requires:[
        'Ext.panel.Panel'
    ],

    src:'rtsp://admin:jsj58568916@10.43.8.51:554/MPEG-4/ch1/main/av_stream',
    socket:'ws://10.99.1.15:90/ws',  
    layout: 'fit',
    tpl: [
        '<video id="{id}-video" src="{src}" autoplay style="width:100%;height:100%"/>'
    ],

    initComponent: function () {
        var me = this;

        var cfg = me.data = Ext.copy({
            tag   : 'video'
        }, me, 'id');
        cfg.src = me.src;

        me.callParent();
    },    
    
    afterRender: function() {
        var me = this;
        me.callParent();
        me.video = me.body.getById(me.id + '-video');
        el = me.video.dom;
		me.player = Streamedian.player(el, {socket:me.socket});
        me.video.on('error', me.onVideoError, me);
    },

    onVideoError: function() {
        var me = this;
 
        me.video.remove();
    },   
    
   
    doDestroy: function () {
        var me = this;
		if(me.player){
			(async() => {
				try {
                    if(me.player.client && me.player.client.clientSM){  
                        for (let session in me.player.client.clientSM.sessions) {
                            await me.player.client.clientSM.sessions[session].sendTeardown();
                        }
                    }
					await me.player.destroy();   
					me.player = null;
				} catch (err) {
					console.log(err);
				}
			})();
		}
		var video = me.video;
        if (video) {
            var videoDom = video.dom;
            if (videoDom) {
                if(videoDom.pause){
                    videoDom.pause();
                }
				videoDom.src = "";
				videoDom.load();
            }
            video.remove();
            me.video = null;
        }
 
        me.callParent();
    }
});
