import "./StudioViewer.css"
import { useState, forwardRef, useImperativeHandle, useRef } from "react";
import { Pagination, Button, Spin, Modal, Descriptions, Input } from 'antd';
import { getMoivesByFilter } from "../services/DataService";
import MovieViewer from "./MovieViewer";

const { Search } = Input;

const studioViewer = forwardRef((props, ref) => {
    const numEachPage = 63;

    const [minValue, setMinValue] = useState(0);
    const [maxValue, setMaxValue] = useState(numEachPage);
    const [studios, setStudios] = useState([]);
    const [originalStudios, setOriginalStudios] = useState([]);
    const [studio, setStudio] = useState(null);
    const [isLoading, setIsLoading] = useState(true);
    const [visible, setVisible] = useState(false);

    const movieViewer = useRef();

    useImperativeHandle(ref, () => ({
        initializeStudios(studios) {
            init(studios);
        },
        setIsLoading() {
            setIsLoading(true);
        }
    }));

    function init(studios) {
        setMinValue(0);
        setMaxValue(numEachPage);
        setStudios(studios);
        if (originalStudios.length === 0) {
            setOriginalStudios(studios);
        }
        setIsLoading(false);
    }

    function handleChange(value) {
        setMinValue((value - 1) * numEachPage);
        setMaxValue(value * numEachPage);
    };

    function showStudioDetails(studioIndex) {
        setStudio(studios[studioIndex]);
        setVisible(true);
        // movieViewer?.current.setIsLoading();
        getMoivesByFilter(5, [studios[studioIndex]], false).then(resp => {
            movieViewer?.current.initializeMovies(resp, 5, studios[studioIndex]);
        });
    }

    function onSearch(value) {
        setIsLoading(true);
        if (value && value.trim()) {
            // Filter studios by search value
            const filteredStudios = originalStudios.filter(studio => 
                studio && studio.toLowerCase().includes(value.toLowerCase())
            );
            init(filteredStudios);
        } else {
            // If search is empty, show all studios
            init(originalStudios);
        }
    }



    return (
        <div className="studio-viewer">
            {isLoading ? <div><Spin size="large" /></div> :
                <Pagination
                    simple
                    defaultCurrent={1}
                    defaultPageSize={numEachPage} //default size of page
                    onChange={handleChange}
                    total={studios?.length}
                    className="header-left"
                />}
            <Search placeholder="工作室名" onSearch={onSearch} className="header-right studio-search-bar" loading={isLoading} />
            {isLoading ? <div><Spin size="large" /></div> :
                <div>
                    <div className="studio-list">
                        {studios?.slice(minValue, maxValue).map((studio, i) =>
                            <Button key={"studio-" + i + minValue} className="studio-button" onClick={() => showStudioDetails(i + minValue)}>
                                <span className="studio-span">{studio || "佚名工作室"}</span>
                            </Button>)}
                    </div>
                </div>}
            <Modal
                title={[<Button key="studio-like-btn"
                    shape="circle"></Button>]}
                centered
                visible={visible}
                onOk={() => setVisible(false)}
                onCancel={() => setVisible(false)}
                width={1100}
                className="studio-details"
            >
                <Descriptions title={studio} bordered>
                </Descriptions>
                <MovieViewer ref={movieViewer} />
            </Modal>
        </div>
    )
});
export default studioViewer;
