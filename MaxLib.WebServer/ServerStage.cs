namespace MaxLib.WebServer
{
    /// <summary>
    /// Defines the stage the service should be executed. At some stages only one service can be
    /// executed. At others all services can be executed even when another one succeeds before.
    /// </summary>
    public enum ServerStage
    {
        /// <summary>
        /// At this stage the request from the client is readed. Only one service is allowed to
        /// execute at this stage.
        /// </summary>
        ReadRequest = 1,
        /// <summary>
        /// At this stage the already readed request is prepared for the 
        /// <see cref="CreateDocument" /> stage. Multiple services can be executed here.
        /// </summary>
        ParseRequest = 2,
        /// <summary>
        /// At this stage the request is executed and a document for the response is created.
        /// Only one service is allowed to execute at this stage.
        /// </summary>
        CreateDocument = 3,
        /// <summary>
        /// At this stage the created document from <see cref="CreateDocument" /> is post processed.
        /// Multiple services can be execuded here.
        /// </summary>
        ProcessDocument = 4,
        /// <summary>
        /// At this stage the response headers are generated and the document is prepared to send 
        /// to the requesting client.
        /// Multiple services can be execuded here.
        /// </summary>
        CreateResponse = 5,
        /// <summary>
        /// At this stage the complete response is transmitted to the client.
        /// Only one service is allowed to execute at this stage.
        /// </summary>
        SendResponse = 6,
        /// <summary>
        /// This stage performs right after <see cref="SendResponse" /> and can be used to cleanup
        /// stuff after the response is transmitted.
        /// Multiple services can be executed here.
        /// </summary>
        Cleanup = 7,
        /// <summary>
        /// This is an alias for <see cref="ReadRequest" /> to specify the first stage.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        FIRST_STAGE = ReadRequest,
        /// <summary>
        /// This is an alias for <see cref="Cleanup" /> to specify the last stage.
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        FINAL_STAGE = Cleanup,
    }
}
